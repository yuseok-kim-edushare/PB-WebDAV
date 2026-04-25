$PBExportHeader$nvo_webdavclient.sru
forward
global type nvo_webdavclient from dotnetobject
end type
end forward

global type nvo_webdavclient from dotnetobject
event ue_error ( )
end type
global nvo_webdavclient nvo_webdavclient

type variables

PUBLIC:
String is_assemblypath = "C:\Source\dll_publish\PBWebDAV.dll"
String is_classname = "PBWebDAV.WebDavClient"

/* Exception handling -- Indicates how proxy handles .NET exceptions */
Boolean ib_CrashOnException = False

/*      Error types       */
Constant Int SUCCESS        =  0 // No error since latest reset
Constant Int LOAD_FAILURE   = -1 // Failed to load assembly
Constant Int CREATE_FAILURE = -2 // Failed to create .NET object
Constant Int CALL_FAILURE   = -3 // Call to .NET function failed

/* Latest error -- Public reset via of_ResetError */
PRIVATEWRITE Long il_ErrorType   
PRIVATEWRITE Long il_ErrorNumber 
PRIVATEWRITE String is_ErrorText 

PRIVATE:
/*  .NET object creation */
Boolean ib_objectCreated

/* Error handler -- Public access via of_SetErrorHandler/of_ResetErrorHandler/of_GetErrorHandler
    Triggers "ue_Error" event for each error when no current error handler */
PowerObject ipo_errorHandler // Each error triggers <ErrorHandler, ErrorEvent>
String is_errorEvent
end variables

forward prototypes
public subroutine of_seterrorhandler (powerobject apo_newhandler, string as_newevent)
public subroutine of_signalerror ()
private subroutine of_setdotneterror (string as_failedfunction, string as_errortext)
public subroutine of_reseterror ()
public function boolean of_createondemand ()
private subroutine of_setassemblyerror (long al_errortype, string as_actiontext, long al_errornumber, string as_errortext)
public subroutine of_geterrorhandler (ref powerobject apo_currenthandler,ref string as_currentevent)
public subroutine of_reseterrorhandler ()
public function boolean of_initialize(string as_baseurl,string as_username,string as_password)
public function boolean of_initializewithproxy(string as_baseurl,string as_username,string as_password,string as_proxyurl,string as_proxyusername,string as_proxypassword)
public subroutine  of_settimeout(long al_timeoutseconds)
public function long of_listdirectory(string as_remotepath)
public function long of_getitemcount()
public function string of_getitemhref(long al_index)
public function string of_getitemdisplayname(long al_index)
public function boolean of_getitemiscollection(long al_index)
public function longlong of_getitemcontentlength(long al_index)
public function string of_getitemcontenttype(long al_index)
public function string of_getitemlastmodified(long al_index)
public function string of_getitemetag(long al_index)
public function string of_getitemcreationdate(long al_index)
public function long of_getitemstatuscode(long al_index)
public function boolean of_downloadfile(string as_remotepath,string as_localpath)
public function boolean of_uploadfile(string as_localpath,string as_remotepath)
public function boolean of_deleteitem(string as_remotepath)
public function boolean of_createdirectory(string as_remotepath)
public function boolean of_copyitem(string as_sourcepath,string as_destpath,boolean abln_overwrite)
public function boolean of_moveitem(string as_sourcepath,string as_destpath,boolean abln_overwrite)
public function boolean of_itemexists(string as_remotepath)
public function string of_getlasterror()
public function long of_getlaststatuscode()
public subroutine  of_dispose()
end prototypes

event ue_error ( );
/*-----------------------------------------------------------------------------------------*/
/*  Handler undefined or call failed (event undefined) => Signal object itself */
/*-----------------------------------------------------------------------------------------*/
end event

public subroutine of_seterrorhandler (powerobject apo_newhandler, string as_newevent);
//*-----------------------------------------------------------------*/
//*    of_seterrorhandler:  
//*                       Register new error handler (incl. event)
//*-----------------------------------------------------------------*/

This.ipo_errorHandler = apo_newHandler
This.is_errorEvent = Trim(as_newEvent)
end subroutine

public subroutine of_signalerror ();
//*-----------------------------------------------------------------------------*/
//* PRIVATE of_SignalError
//* Triggers error event on previously defined error handler.
//* Calls object's own UE_ERROR when handler or its event is undefined.
//*
//* Handler is "DEFINED" when
//* 	1) <ErrorEvent> is non-empty
//*	2) <ErrorHandler> refers to valid object
//*	3) <ErrorEvent> is actual event on <ErrorHandler>
//*-----------------------------------------------------------------------------*/

Boolean lb_handlerDefined
If This.is_errorEvent > '' Then
	If Not IsNull(This.ipo_errorHandler) Then
		lb_handlerDefined = IsValid(This.ipo_errorHandler)
	End If
End If

If lb_handlerDefined Then
	/* Try to call defined handler*/
	Long ll_status
	ll_status = This.ipo_errorHandler.TriggerEvent(This.is_errorEvent)
	If ll_status = 1 Then Return
End If

/* Handler undefined or call failed (event undefined) => Signal object itself*/
This.event ue_Error( )
end subroutine

private subroutine of_setdotneterror (string as_failedfunction, string as_errortext);
//*----------------------------------------------------------------------------------------*/
//* PRIVATE of_setDotNETError
//* Sets error description for specified error condition exposed by call to .NET  
//*
//* Error description layout
//*			| Call <failedFunction> failed.<EOL>
//*			| Error Text: <errorText> (*)
//* (*): Line skipped when <ErrorText> is empty
//*----------------------------------------------------------------------------------------*/

/* Format description*/
String ls_error
ls_error = "Call " + as_failedFunction + " failed."
If Len(Trim(as_errorText)) > 0 Then
	ls_error += "~r~nError Text: " + as_errorText
End If

/* Retain state in instance variables*/
This.il_ErrorType = This.CALL_FAILURE
This.is_ErrorText = ls_error
This.il_ErrorNumber = 0
end subroutine

public subroutine of_reseterror ();
//*--------------------------------------------*/
//* PUBLIC of_ResetError
//* Clears previously registered error
//*--------------------------------------------*/

This.il_ErrorType = This.SUCCESS
This.is_ErrorText = ''
This.il_ErrorNumber = 0
end subroutine

public function boolean of_createondemand ();
//*--------------------------------------------------------------*/
//*  PUBLIC   of_createOnDemand( )
//*  Return   True:  .NET object created
//*               False: Failed to create .NET object
//*  Loads .NET assembly and creates instance of .NET class.
//*  Uses .NET Framework when loading .NET assembly.
//*  Signals error If an error occurs.
//*  Resets any prior error when load + create succeeds.
//*--------------------------------------------------------------*/

This.of_ResetError( )
If This.ib_objectCreated Then Return True // Already created => DONE

Long ll_status 
String ls_action

/* Load assembly using .NET Framework */
ls_action = 'Load ' + This.is_AssemblyPath
DotNetAssembly lnv_assembly
lnv_assembly = Create DotNetAssembly
ll_status = lnv_assembly.LoadWithDotNetFramework(This.is_AssemblyPath)

/* Abort when load fails */
If ll_status <> 1 Then
	This.of_SetAssemblyError(This.LOAD_FAILURE, ls_action, ll_status, lnv_assembly.ErrorText)
	This.of_SignalError( )
	Return False // Load failed => ABORT
End If

/*   Create .NET object */
ls_action = 'Create ' + This.is_ClassName
ll_status = lnv_assembly.CreateInstance(is_ClassName, This)

/* Abort when create fails */
If ll_status <> 1 Then
	This.of_SetAssemblyError(This.CREATE_FAILURE, ls_action, ll_status, lnv_assembly.ErrorText)
	This.of_SignalError( )
	Return False // Load failed => ABORT
End If

This.ib_objectCreated = True
Return True
end function

private subroutine of_setassemblyerror (long al_errortype, string as_actiontext, long al_errornumber, string as_errortext);
//*----------------------------------------------------------------------------------------------*/
//* PRIVATE of_setAssemblyError
//* Sets error description for specified error condition report by an assembly function
//*
//* Error description layout
//* 		| <actionText> failed.<EOL>
//* 		| Error Number: <errorNumber><EOL>
//* 		| Error Text: <errorText> (*)
//*  (*): Line skipped when <ErrorText> is empty
//*----------------------------------------------------------------------------------------------*/

/*    Format description */
String ls_error
ls_error = as_actionText + " failed.~r~n"
ls_error += "Error Number: " + String(al_errorNumber) + "."
If Len(Trim(as_errorText)) > 0 Then
	ls_error += "~r~nError Text: " + as_errorText
End If

/*  Retain state in instance variables */
This.il_ErrorType = al_errorType
This.is_ErrorText = ls_error
This.il_ErrorNumber = al_errorNumber
end subroutine

public subroutine of_geterrorhandler (ref powerobject apo_currenthandler,ref string as_currentevent);
//*-------------------------------------------------------------------------*/
//* PUBLIC of_GetErrorHandler
//* Return as REF-parameters current error handler (incl. event)
//*-------------------------------------------------------------------------*/

apo_currentHandler = This.ipo_errorHandler
as_currentEvent = This.is_errorEvent
end subroutine

public subroutine of_reseterrorhandler ();
//*---------------------------------------------------*/
//* PUBLIC of_ResetErrorHandler
//* Removes current error handler (incl. event)
//*---------------------------------------------------*/

SetNull(This.ipo_errorHandler)
SetNull(This.is_errorEvent)
end subroutine

public function boolean of_initialize(string as_baseurl,string as_username,string as_password);
//*-----------------------------------------------------------------*/
//*  .NET function : Initialize
//*   Argument:
//*              String as_baseurl
//*              String as_username
//*              String as_password
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "Initialize"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.initialize(as_baseurl,as_username,as_password)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_initializewithproxy(string as_baseurl,string as_username,string as_password,string as_proxyurl,string as_proxyusername,string as_proxypassword);
//*-----------------------------------------------------------------*/
//*  .NET function : InitializeWithProxy
//*   Argument:
//*              String as_baseurl
//*              String as_username
//*              String as_password
//*              String as_proxyurl
//*              String as_proxyusername
//*              String as_proxypassword
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "InitializeWithProxy"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.initializewithproxy(as_baseurl,as_username,as_password,as_proxyurl,as_proxyusername,as_proxypassword)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public subroutine  of_settimeout(long al_timeoutseconds);
//*-----------------------------------------------------------------*/
//*  .NET function : SetTimeout
//*   Argument:
//*              Long al_timeoutseconds
//*   Return : (None)
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function

/* Set the dotnet function name */
ls_function = "SetTimeout"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		Return 
	End If

	/* Trigger the dotnet function */
	This.settimeout(al_timeoutseconds)
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

End Try
end subroutine

public function long of_listdirectory(string as_remotepath);
//*-----------------------------------------------------------------*/
//*  .NET function : ListDirectory
//*   Argument:
//*              String as_remotepath
//*   Return : Long
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Long ll_result

/* Set the dotnet function name */
ls_function = "ListDirectory"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ll_result)
		Return ll_result
	End If

	/* Trigger the dotnet function */
	ll_result = This.listdirectory(as_remotepath)
	Return ll_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ll_result)
	Return ll_result
End Try
end function

public function long of_getitemcount();
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemCount
//*   Return : Long
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Long ll_result

/* Set the dotnet function name */
ls_function = "GetItemCount"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ll_result)
		Return ll_result
	End If

	/* Trigger the dotnet function */
	ll_result = This.getitemcount()
	Return ll_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ll_result)
	Return ll_result
End Try
end function

public function string of_getitemhref(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemHref
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemHref"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemhref(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function string of_getitemdisplayname(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemDisplayName
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemDisplayName"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemdisplayname(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function boolean of_getitemiscollection(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemIsCollection
//*   Argument:
//*              Long al_index
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "GetItemIsCollection"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.getitemiscollection(al_index)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function longlong of_getitemcontentlength(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemContentLength
//*   Argument:
//*              Long al_index
//*   Return : Longlong
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Longlong lll_result

/* Set the dotnet function name */
ls_function = "GetItemContentLength"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lll_result)
		Return lll_result
	End If

	/* Trigger the dotnet function */
	lll_result = This.getitemcontentlength(al_index)
	Return lll_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lll_result)
	Return lll_result
End Try
end function

public function string of_getitemcontenttype(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemContentType
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemContentType"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemcontenttype(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function string of_getitemlastmodified(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemLastModified
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemLastModified"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemlastmodified(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function string of_getitemetag(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemETag
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemETag"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemetag(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function string of_getitemcreationdate(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemCreationDate
//*   Argument:
//*              Long al_index
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetItemCreationDate"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getitemcreationdate(al_index)
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function long of_getitemstatuscode(long al_index);
//*-----------------------------------------------------------------*/
//*  .NET function : GetItemStatusCode
//*   Argument:
//*              Long al_index
//*   Return : Long
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Long ll_result

/* Set the dotnet function name */
ls_function = "GetItemStatusCode"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ll_result)
		Return ll_result
	End If

	/* Trigger the dotnet function */
	ll_result = This.getitemstatuscode(al_index)
	Return ll_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ll_result)
	Return ll_result
End Try
end function

public function boolean of_downloadfile(string as_remotepath,string as_localpath);
//*-----------------------------------------------------------------*/
//*  .NET function : DownloadFile
//*   Argument:
//*              String as_remotepath
//*              String as_localpath
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "DownloadFile"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.downloadfile(as_remotepath,as_localpath)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_uploadfile(string as_localpath,string as_remotepath);
//*-----------------------------------------------------------------*/
//*  .NET function : UploadFile
//*   Argument:
//*              String as_localpath
//*              String as_remotepath
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "UploadFile"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.uploadfile(as_localpath,as_remotepath)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_deleteitem(string as_remotepath);
//*-----------------------------------------------------------------*/
//*  .NET function : DeleteItem
//*   Argument:
//*              String as_remotepath
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "DeleteItem"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.deleteitem(as_remotepath)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_createdirectory(string as_remotepath);
//*-----------------------------------------------------------------*/
//*  .NET function : CreateDirectory
//*   Argument:
//*              String as_remotepath
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "CreateDirectory"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.createdirectory(as_remotepath)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_copyitem(string as_sourcepath,string as_destpath,boolean abln_overwrite);
//*-----------------------------------------------------------------*/
//*  .NET function : CopyItem
//*   Argument:
//*              String as_sourcepath
//*              String as_destpath
//*              Boolean abln_overwrite
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "CopyItem"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.copyitem(as_sourcepath,as_destpath,abln_overwrite)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_moveitem(string as_sourcepath,string as_destpath,boolean abln_overwrite);
//*-----------------------------------------------------------------*/
//*  .NET function : MoveItem
//*   Argument:
//*              String as_sourcepath
//*              String as_destpath
//*              Boolean abln_overwrite
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "MoveItem"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.moveitem(as_sourcepath,as_destpath,abln_overwrite)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function boolean of_itemexists(string as_remotepath);
//*-----------------------------------------------------------------*/
//*  .NET function : ItemExists
//*   Argument:
//*              String as_remotepath
//*   Return : Boolean
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Boolean lbln_result

/* Set the dotnet function name */
ls_function = "ItemExists"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(lbln_result)
		Return lbln_result
	End If

	/* Trigger the dotnet function */
	lbln_result = This.itemexists(as_remotepath)
	Return lbln_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(lbln_result)
	Return lbln_result
End Try
end function

public function string of_getlasterror();
//*-----------------------------------------------------------------*/
//*  .NET function : GetLastError
//*   Return : String
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
String ls_result

/* Set the dotnet function name */
ls_function = "GetLastError"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ls_result)
		Return ls_result
	End If

	/* Trigger the dotnet function */
	ls_result = This.getlasterror()
	Return ls_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ls_result)
	Return ls_result
End Try
end function

public function long of_getlaststatuscode();
//*-----------------------------------------------------------------*/
//*  .NET function : GetLastStatusCode
//*   Return : Long
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function
Long ll_result

/* Set the dotnet function name */
ls_function = "GetLastStatusCode"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		SetNull(ll_result)
		Return ll_result
	End If

	/* Trigger the dotnet function */
	ll_result = This.getlaststatuscode()
	Return ll_result
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

	/*  Indicate error occurred */
	SetNull(ll_result)
	Return ll_result
End Try
end function

public subroutine  of_dispose();
//*-----------------------------------------------------------------*/
//*  .NET function : Dispose
//*   Return : (None)
//*-----------------------------------------------------------------*/
/* .NET  function name */
String ls_function

/* Set the dotnet function name */
ls_function = "Dispose"

Try
	/* Create .NET object */
	If Not This.of_createOnDemand( ) Then
		Return 
	End If

	/* Trigger the dotnet function */
	This.dispose()
Catch(runtimeerror re_error)

	If This.ib_CrashOnException Then Throw re_error

	/*   Handle .NET error */
	This.of_SetDotNETError(ls_function, re_error.text)
	This.of_SignalError( )

End Try
end subroutine

on nvo_webdavclient.create
call super::create
triggerevent( this, "constructor" )
end on

on nvo_webdavclient.destroy
triggerevent( this, "destructor" )
call super::destroy
end on

