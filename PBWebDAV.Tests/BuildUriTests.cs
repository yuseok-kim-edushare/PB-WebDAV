using System;
using Xunit;
using PBWebDAV;

namespace PBWebDAV.Tests
{
    public class BuildUriTests
    {
        [Fact]
        public void Initialize_And_Send_Invalid_Host_Throws_Or_False()
        {
            var client = new WebDavClient();
            Assert.True(client.Initialize("http://localhost:8080/webdav", "user", "pass"));
            
            // Since BuildUri throws InvalidOperationException which is unhandled in ItemExists RunSync,
            // or handled as false? 
            // In ItemExists RunSync it's not wrapped in a try/catch, so it will throw the exception out
            
            Assert.Throws<InvalidOperationException>(() => client.ItemExists("http://attacker.com/malicious"));
        }

        [Fact]
        public void Initialize_And_Send_Valid_Host_Does_Not_Throw_SSRF()
        {
            var client = new WebDavClient();
            Assert.True(client.Initialize("http://localhost:8080/webdav", "user", "pass"));
            
            // Expected to fail normally due to no server, not InvalidOperationException
            try
            {
                client.ItemExists("http://localhost:8080/safe");
                // it might return false or throw HttpRequestException since there's no server
            }
            catch (Exception ex)
            {
                Assert.IsNotType<InvalidOperationException>(ex);
            }
        }
    }
}
