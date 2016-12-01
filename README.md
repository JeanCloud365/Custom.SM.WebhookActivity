## Proudly announcing: The Webhook Activity Management Pack

What does this management pack do? It adds an extra activity 'Webhook Activity' type to the SCSM mix. When set to 'in progress' it will send configurable data to a webhook url where the underlying service can consime it (like Azure Automation).

This actvity allows you to configure the following parameters:

* URL: a webhook url to call when the activity is put 'in progress' by SCSM
* 10 Parameter Name/Value pairs where you can assign 'Name'-properties in a SR or Activity template for example, and link the 'Value'-properties in a Service Offering
* Wait For Callback: the activity will either wait until the runbook that is triggered by the webhook updates it (true), or immediately completes (false).
* Include Activity ID: If set to true, includes the Activity's GUID in the data sent to the webhook
* Activity ID Parameter: Allows you to specify the parameter-name for the activity-id (default: ActivityID)

# How does it work?

When a webhook activity is set to 'in progress' by the SCSM workflow engine, it will trigger a custom workflow that will do the following:

* Get the Activity properties
* Encode parameter name/value fields that are filled-in into a JSON array
* If specified, add the Activity ID
* Submit the JSON data to the webhook URL
* Update the statustext field with either the received web response (for Azure Automation this is the job id), or an error message in case of issues
* If 'Wait for Callback' is not enabled, the activity will be set to 'completed'
* In case of an error, the activity is set to 'failed' regardless of the 'Wait for Callback' setting


# Requirements

* For the JSON encoding, an external DLL is used. It is packaged in the release.
* The workflow server must have web access towards the used webhook URL's
* The workflow account must have advanced author rights in SCSM
* SCSM 2016 (tested) or 2012 R2 (shoud work, else notify me :))

# Install Instructions

* Import the management pack
* Copy to the 2 DLL's inside the SCSM Workflow Server's installation folder
    * If you are upgrading this management pack, stop the 'Microsoft Monitoring Agent' service before copying the DLL's. Restart it afterwards.
* You can now create templates containing webhook activities. Enjoy!

I also included 2 example runbooks that work together to execute the IT process and callback to the webhook activity (only tested on a hybrid worker).
