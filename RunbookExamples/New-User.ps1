 param (
        [object]$WebhookData
    )
    $VerbosePreference = "Continue"
    # If runbook was called from Webhook, WebhookData will not be null.
    if ($WebhookData -ne $null) {

        # Collect properties of WebhookData
        $WebhookName     =     $WebhookData.WebhookName
        $WebhookHeaders =     $WebhookData.RequestHeader
        $WebhookBody     =     $WebhookData.RequestBody
    }
    try{
        $ErrorActionPreference = "stop"
		# Convert the JSON data posted by the SCSM Webhook Activity to a PS object
        $userInfo = ConvertFrom-Json -InputObject $WebhookBody
        Write-Verbose $userInfo
    
		# Get the automation credential from Azure Automation and use it to create the user in Azure AD
        $cred = Get-AutomationPSCredential -Name "AutomationAccount"
        Connect-MSOLService -Credential $cred
        $Principal = "$($userInfo.First).$($userInfo.Last)@jvmscugdemo.onmicrosoft.com"
        
       
        $User = New-MsolUser -UserPrincipalName $Principal -DisplayName $Principal -UsageLocation $userInfo.Country -MobilePhone $userInfo.Mobile
        if($userInfo.License -eq "Yes"){
            Set-MsolUserLicense -UserPrincipalName $Principal -AddLicenses "jvmscugdemo:INTUNE_A","jvmscugdemo:AAD_PREMIUM_P2","jvmscugdemo:ENTERPRISEPREMIUM"
        }
		# Call the SCSM update script inline
        .\Set-SCSMRequest.ps1 -ActivityId $userInfo.ActivityId -Status Completed -Comment "User $($userInfo.First) created with parameters login '$Principal' / password '$($User.Password)'"
    }
    catch {
	# If an issue happens, update the SCSM ticket accordingly as well
        write-verbose "Error: $($_)"
        .\Set-SCSMRequest.ps1 -ActivityId $userInfo.ActivityId -Status Failed -Comment "$($_)"

    }