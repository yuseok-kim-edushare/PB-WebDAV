$PBExportHeader$pb-webdav-sample.sra
$PBExportComments$Generated Application Object
forward
global type pb-webdav-sample from application
end type
global transaction sqlca
global dynamicdescriptionarea sqlda
global dynamicstagingarea sqlsa
global error error
global message message
end forward

global type pb-webdav-sample from application
string appname = "pb-webdav-sample"
string appruntimeversion = "19.2.0.2728"
end type
global pb-webdav-sample pb-webdav-sample

on pb-webdav-sample.create
appname = "pb-webdav-sample"
message = create message
sqlca = create transaction
sqlda = create dynamicdescriptionarea
sqlsa = create dynamicstagingarea
error = create error
end on

on pb-webdav-sample.destroy
destroy( sqlca )
destroy( sqlda )
destroy( sqlsa )
destroy( error )
destroy( message )
end on

