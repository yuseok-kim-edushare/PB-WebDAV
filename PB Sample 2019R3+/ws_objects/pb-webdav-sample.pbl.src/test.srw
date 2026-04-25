$PBExportHeader$test.srw
forward
global type test from window
end type
end forward

global type test from window
integer width = 3881
integer height = 1856
boolean titlebar = true
string title = "Untitled"
boolean controlmenu = true
boolean minbox = true
boolean maxbox = true
boolean resizable = true
long backcolor = 553648127
string icon = "AppIcon!"
boolean center = true
end type
global test test

type variables
String is_webdav_base_url = "your web dev sites (We recommand Https"
String is_webdav_user = "your id"
String is_webdav_password = "your password"
String is_contract_remote_dir = "/your's directory"
end variables

forward prototypes
public function string uf_webdav_error (nvo_webdavclient anv_client)
public function integer uf_webdav_upload (string as_localfile, string as_remotefile)
public function integer uf_webdav_download (string as_remotefile, string as_localfile)
public function integer uf_webdav_delete (string as_remotefile)
public function integer uf_webdav_list (string as_remote_dir)
end prototypes

public function string uf_webdav_error (nvo_webdavclient anv_client);String ls_error

if isvalid(anv_client) then
	ls_error = anv_client.of_getlasterror()
	
	if isnull(ls_error) or trim(ls_error) = '' then
		ls_error = anv_client.is_errortext
	end if
end if

if isnull(ls_error) or trim(ls_error) = '' then
	ls_error = "Error occurred during WebDAV processing."
end if

return ls_error
end function

public function integer uf_webdav_upload (string as_localfile, string as_remotefile);nvo_webdavclient lnv_client
Boolean lb_rtn, lb_exists
String  ls_error

lnv_client = CREATE nvo_webdavclient

lb_rtn = lnv_client.of_initialize(is_webdav_base_url, is_webdav_user, is_webdav_password)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed to Connect WebDAV !!~r~n~r~n" + ls_error)
	return 1
end if

lb_exists = lnv_client.of_itemexists(is_contract_remote_dir)
if not isnull(lb_exists) then
	if not lb_exists then
		lnv_client.of_createdirectory(is_contract_remote_dir)
	end if
end if

lb_rtn = lnv_client.of_uploadfile(as_localfile, as_remotefile)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed to upload file !!~r~n~r~n" + ls_error)
	return 1
end if

lnv_client.of_dispose()
DESTROY lnv_client

return 0
end function

public function integer uf_webdav_download (string as_remotefile, string as_localfile);nvo_webdavclient lnv_client
Boolean lb_rtn
String  ls_error

lnv_client = CREATE nvo_webdavclient

lb_rtn = lnv_client.of_initialize(is_webdav_base_url, is_webdav_user, is_webdav_password)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed To connect WebDAV !!~r~n~r~n" + ls_error)
	return 1
end if

lb_rtn = lnv_client.of_downloadfile(as_remotefile, as_localfile)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed to Download file !!~r~n~r~n" + ls_error)
	return 1
end if

lnv_client.of_dispose()
DESTROY lnv_client

return 0
end function

public function integer uf_webdav_delete (string as_remotefile);nvo_webdavclient lnv_client
Boolean lb_rtn
String  ls_error

lnv_client = CREATE nvo_webdavclient

lb_rtn = lnv_client.of_initialize(is_webdav_base_url, is_webdav_user, is_webdav_password)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed To connect WebDAV !!~r~n~r~n" + ls_error)
	return 1
end if

lb_rtn = lnv_client.of_deleteitem(as_remotefile)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed to Delete file/folder !!~r~n~r~n" + ls_error)
	return 1
end if

lnv_client.of_dispose()
DESTROY lnv_client

return 0
end function

public function integer uf_webdav_list (string as_remote_dir);nvo_webdavclient lnv_client
Boolean lb_rtn
Integer li_count, i
Long    ll_size
String  ls_error, ls_filename, ls_info

lnv_client = CREATE nvo_webdavclient

lb_rtn = lnv_client.of_initialize(is_webdav_base_url, is_webdav_user, is_webdav_password)
if isnull(lb_rtn) then lb_rtn = false

if not lb_rtn then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed To connect WebDAV !!~r~n~r~n" + ls_error)
	return -1
end if

// List directory contents (demonstrates handling the -1 error return value)
li_count = lnv_client.of_listdirectory(as_remote_dir)
if isnull(li_count) then li_count = -1

if li_count < 0 then
	ls_error = uf_webdav_error(lnv_client)
	lnv_client.of_dispose()
	DESTROY lnv_client
	messagebox("Notice", "Failed to list directory !!~r~n~r~n" + ls_error)
	return -1
end if

// Loop through the retrieved items (0-based index) and extract info
ls_info = "Found " + string(li_count) + " items.~r~n~r~n"

// Limit output to a maximum of 10 items to prevent a huge MessageBox
for i = 0 to li_count - 1
	ls_filename = lnv_client.of_getitemdisplayname(i)
	ll_size = lnv_client.of_getitemcontentlength(i)
	
	ls_info += "[" + string(i+1) + "] " + ls_filename + " (" + string(ll_size) + " bytes)~r~n"
	
	if i >= 9 then
		ls_info += "... and more."
		exit
	end if
next

messagebox("Directory Listing", ls_info)

lnv_client.of_dispose()
DESTROY lnv_client

return li_count
end function

on test.create
end on

on test.destroy
end on

