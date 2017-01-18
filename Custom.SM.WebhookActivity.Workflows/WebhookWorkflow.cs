using Microsoft.EnterpriseManagement;
using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;
// Newtonsoft.JSon is used for the JSON encoding of the activity parameters: http://www.newtonsoft.com/json/
using Newtonsoft.Json;
using System.IO;
using Microsoft.EnterpriseManagement.WorkflowFoundation;

namespace Custom.SM.WebhookActivity.Workflows
{
    public class WebhookWorkflow:SequentialWorkflowActivity
    {
        // The only parameter is the ID of the SCSM Activity ID
        public static DependencyProperty InstanceIdProperty = DependencyProperty.Register("InstanceId", typeof(string), typeof(WebhookWorkflow));
        public string InstanceId { get; set; }

        // Setup some variables for use in the processing code
        private ManagementPackClass mpcWebhookActivity;
        private ManagementPackEnumeration mpeActivityCompleted;
        private ManagementPackEnumeration mpeActivityFailed;
        private EnterpriseManagementGroup emg;
        EnterpriseManagementObject emoWebhookActivity;





        protected override ActivityExecutionStatus Execute(ActivityExecutionContext executionContext)
        {
            try
            {
                // Obtain the MG object from the SCSM Workflow framework
                emg = ServerContextHelper.Instance.ManagementGroup;
                // Get class and enum definitions to correctly process the activity
                mpcWebhookActivity = emg.EntityTypes.GetClass(new Guid("80bc2a93-1ce0-9773-c37d-f7c59fe7c963"));
                mpeActivityCompleted = emg.EntityTypes.GetEnumeration(new Guid("9de908a1-d8f1-477e-c6a2-62697042b8d9"));
                mpeActivityFailed = emg.EntityTypes.GetEnumeration(new Guid("144bcd52-a710-2778-2a6e-c62e0c8aae74"));
                // Retrieve the Activity from the SDK
                emoWebhookActivity = emg.EntityObjects.GetObject<EnterpriseManagementObject>(new Guid(InstanceId), ObjectQueryOptions.Default);
                // NameValueCollection is an alternative of using JSON, but it would post plaintext data to the webhook instead of a defined structure. This would make it harder to parse the data in the runbook.
                NameValueCollection nvcParameters = new NameValueCollection();
                // Setup the JSON Writer: http://www.newtonsoft.com/json/help/html/ReadingWritingJSON.htm
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                JsonWriter jw = new JsonTextWriter(sw);
                jw.WriteStartObject();
                for (int i = 1; i <= 10; i++)
                {
                    // Capture all Activity Parameter Name/Value sets that are not null and put them in the JSON array
                    if (emoWebhookActivity[mpcWebhookActivity, "ParameterName" + i].Value != null)
                    {
                        string parameter = emoWebhookActivity[mpcWebhookActivity, "ParameterName" + i].Value.ToString();
                        string value = emoWebhookActivity[mpcWebhookActivity, "ParameterValue" + i].Value.ToString();
                        nvcParameters.Add(parameter, value);
                        jw.WritePropertyName(parameter);
                        jw.WriteValue(value);


                    }
                }
               
                // If specified in the activity, add the activity id in there too
                if ((bool)emoWebhookActivity[mpcWebhookActivity, "IncludeActivityId"].Value == true)
                {
                    string parameter = emoWebhookActivity[mpcWebhookActivity, "ActivityIdParameterName"].Value.ToString();
                    string value = emoWebhookActivity.Id.ToString();
                    nvcParameters.Add(parameter, value);
                    jw.WritePropertyName(parameter);
                    jw.WriteValue(value);
                }

                jw.WriteEndObject();
                
                // Submit the data to the webhook using a WebClient
                Uri uri = new Uri(emoWebhookActivity[mpcWebhookActivity, "URL"].Value.ToString());
                UTF8Encoding _encoding = new UTF8Encoding();
                using (WebClient wc = new WebClient())
                {
                    
                    var response = wc.UploadString(uri, "POST", sb.ToString());
                    //var stringResponse = _encoding.GetString(response);

                    var matches = Regex.Match(response, Regex.Escape("{") + "\"JobIds\"" + Regex.Escape(":") + Regex.Escape("[") + "\"([^\"]+)\"" + Regex.Escape("]") + Regex.Escape("}"));
                    if (matches.Captures.Count < 1)
                    {
                        throw new InvalidOperationException("Unknown Webhook failure");

                    }
                    else
                    {
                        var jobId = matches.Captures[0];
                        bool ok = false;
                        int retry = 5;
                        // obsolete code to prevent colission issues, not needed anymore as the bug was fixed in the workflow MP
                        while (!ok && retry > 0)
                        {
                            try
                            {
                                // Update the activity with the job info returned from the webhook call
                                emoWebhookActivity[mpcWebhookActivity, "StatusText"].Value = "Webhook Triggered with Job ID '" + jobId + "'";
                                ok = true;
                            }
                            catch (Microsoft.EnterpriseManagement.Common.DiscoveryDataModificationCollisionException ex)
                            {
                                retry--;
                                emoWebhookActivity = emg.EntityObjects.GetObject<EnterpriseManagementObject>(new Guid(InstanceId), ObjectQueryOptions.Default);
                            }
                        }
                    }


                }
                // If the activity must wait for the runbook to update it, end here. Else, set the activity to completed.
                if (!(bool)emoWebhookActivity[mpcWebhookActivity, "WaitForCallBack"].Value)
                {
                    emoWebhookActivity[mpcWebhookActivity, "Status"].Value = mpeActivityCompleted;

                }
                return ActivityExecutionStatus.Closed;


            } catch(Exception ex)
            {
                // in case of troubles, update the activity accordingly
                emoWebhookActivity[mpcWebhookActivity, "StatusText"].Value = ex.Message;

                emoWebhookActivity[mpcWebhookActivity, "Status"].Value = mpeActivityFailed;
                return ActivityExecutionStatus.Closed;

            }
            finally
            {
                // in both OK and NOK situations, save the status info back to the activity
                emoWebhookActivity.Commit();
            }

            
        }

    }
}
