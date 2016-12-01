param(
    $ActivityId,
    $Status,
    $Comment
)
# Import the smlets for easier SCSM management
import-module smlets
# Get the necessary info from the Azure Automation Assets
$cred = Get-AutomationPSCredential -Name "AutomationAccount"
$srv = Get-AutomationVariable -Name "SCSMServer"
# Setup default parameters so that all SCSM commands are correctly send to the SCSM management server
$PSDefaultParameterValues = @{
    "*-SCSM*:Credential"=$cred;
    "*-SCSM*:ComputerName"=$srv
}

# Using the SCSM Webhook Activities ID, fetch the associated service request
$objActivity = Get-SCSMObject -Id $ActivityId
$clsRequest = Get-SCSMClass -Name System.WorkItem.ServiceRequest
$relRequestContainsActivity = Get-SCSMRelationshipClass -Name System.WorkItemContainsActivity

$objRequest = (Get-SCSMRelationshipObject -ByTarget $objActivity|where{$_.SourceObject.ClassName -eq 'System.WorkItem.ServiceRequest'}).SourceObject
# If a comment was passed to the runbook, place it in the SR through a projection
if($Comment){
    $NewGUID = ([Guid]::NewGuid()).ToString()
    $Projection = @{__CLASS = "System.WorkItem.ServiceRequest";
                    __SEED = $objRequest;
                    AnalystCommentLog = @{__CLASS = "System.WorkItem.TroubleTicket.AnalystCommentLog";
                                            __OBJECT = @{Id = $NewGUID;
                                                        DisplayName = $NewGUID;
                                                        Comment = $Comment;
                                                        EnteredBy  = "SYSTEM";
                                                        EnteredDate = (Get-Date).ToUniversalTime();
                                                        IsPrivate = $false
                                                        }
                                            }
                    }

    New-SCSMObjectProjection -Type "System.WorkItem.ServiceRequestProjection" -Projection $Projection
}
# If a status change was passed to the runbook, update the SR status accordingly
if($Status){
    Set-SCSMObject -SMObject $objActivity -Property Status -Value $Status
}