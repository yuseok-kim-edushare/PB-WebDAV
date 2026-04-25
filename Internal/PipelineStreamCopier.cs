using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PBWebDAV.Internal
{
    /// <summary>
    /// Memory-optimal, allocation-minimal stream copy utilities built on
    /// <see cref="System.IO.Pipelines"/>.
    ///
    /// Key benefits over a naive byte[] copy loop:
    ///  • Reads the source in reusable, pool-backed buffers (no per-chunk allocation).
    ///  • Uses <see cref="ArrayPool{T}.Shared"/> only when the pipe's internal
    ///    segment is not already array-backed (i.e. <em>zero copy</em> for the
    ///    common case where it is).
    ///  • All I/O is async so the thread pool is not blocked during network/disk waits.
    /// </summary>
    internal static class PipelineStreamCopier
    {
        private const int BufferSize     = 65_536; // 64 KB pipe segments
        private const int MinimumReadSize =  4_096;

        // ── Public entry points ───────────────────────────────────────────────

        /// <summary>
        /// Reads <paramref name="source"/> (e.g. an HTTP response stream) via a
        /// <see cref="PipeReader"/> and writes it to a new local file.
        /// </summary>
        internal static async Task CopyToFileAsync(
            Stream source,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: BufferSize,
                useAsync: true);

            await CopyViaPipelineAsync(source, fileStream, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a local file via a <see cref="PipeReader"/> and writes it to
        /// <paramref name="destination"/> (e.g. an <c>HttpRequestMessage</c> content stream).
        /// </summary>
        internal static async Task CopyFromFileAsync(
            string sourcePath,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: BufferSize,
                useAsync: true);

            await CopyViaPipelineAsync(fileStream, destination, cancellationToken).ConfigureAwait(false);
        }

        // ── Core pipeline loop ────────────────────────────────────────────────

        private static async Task CopyViaPipelineAsync(
            Stream source,
            Stream destination,
            CancellationToken cancellationToken)
        {
            // PipeReader wraps the source stream; it manages its own internal buffer pool.
            var reader = PipeReader.Create(source, new StreamPipeReaderOptions(
                bufferSize:     BufferSize,
                minimumReadSize: MinimumReadSize,
                leaveOpen:      true));   // caller owns the source stream lifetime

            try
            {
                while (true)
                {
                    ReadResult result = await reader
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);

                    ReadOnlySequence<byte> buffer = result.Buffer;

                    // Walk the (possibly multi-segment) sequence and flush each segment
                    // to the destination without copying when the segment is already array-backed.
                    foreach (ReadOnlyMemory<byte> segment in buffer)
                    {
                        await WriteSegmentAsync(destination, segment, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    // Tell the pipe we consumed the whole buffer.
                    reader.AdvanceTo(buffer.End);

                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            finally
            {
                // Complete() signals the pipe for clean-up; never throws.
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }

        // ── Segment write — zero-copy if possible ─────────────────────────────

        private static Task WriteSegmentAsync(
            Stream destination,
            ReadOnlyMemory<byte> segment,
            CancellationToken cancellationToken)
        {
            // Fast path: the Memory<byte> is backed by a managed array — no copy needed.
            if (MemoryMarshal.TryGetArray(segment, out ArraySegment<byte> arraySegment))
            {
                return destination.WriteAsync(
                    arraySegment.Array!,
                    arraySegment.Offset,
                    arraySegment.Count,
                    cancellationToken);
            }

            // Slow path: memory is backed by native memory or another allocator.
            // Rent a temporary buffer from the shared pool rather than allocating.
            return WriteSegmentViaPoolAsync(destination, segment, cancellationToken);
        }

        private static async Task WriteSegmentViaPoolAsync(
            Stream destination,
            ReadOnlyMemory<byte> segment,
            CancellationToken cancellationToken)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(segment.Length);
            try
            {
                segment.CopyTo(rented);
                await destination
                    .WriteAsync(rented, 0, segment.Length, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
