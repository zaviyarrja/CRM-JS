using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Auto_Num_plugin
{
    public class CommandNumSet : IPlugin
    {
        private static readonly Guid LOCK_RECORD_GUID = new Guid("5c8fee90-8821-e411-941e-001dd8b7210d");

       
        public void Execute(IServiceProvider serviceProvider)
        {
            
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity caseEntity = (Entity)context.InputParameters["Target"];

                if (context.MessageName != "Create") return;

                tracingService.Trace("Plugin started.");

                if (!caseEntity.Attributes.Contains("new_parentcaseid"))
                {
                    tracingService.Trace("Processing as parent case.");
                    GenerateParentCommandNumber(service, tracingService, caseEntity);
                }
                else
                {
                    tracingService.Trace("Processing as child case.");
                    GenerateChildCommandNumber(service, tracingService, caseEntity);
                }

                tracingService.Trace("Plugin execution completed.");
            }
        }

        private void GenerateParentCommandNumber(IOrganizationService service, ITracingService trace, Entity caseEntity)
        {

            trace.Trace("Processing as parent case.");

            QueryExpression query = new QueryExpression("new_case")
            {
                ColumnSet = new ColumnSet("new_parentcommandnumber"),
                Orders =
            {
                new OrderExpression("new_parentcommandnumber", OrderType.Descending)
            }
            };

            EntityCollection results = service.RetrieveMultiple(query);
            int newNumber = 0; // Default starting number

            if (results.Entities.Count > 0 && results.Entities[0].Attributes.Contains("new_parentcommandnumber"))
            {
                int maxValue = results.Entities[0].GetAttributeValue<int>("new_parentcommandnumber");
                newNumber = maxValue + 1;
            }

            caseEntity["new_commandnumber"] = newNumber.ToString();
            caseEntity["new_parentcommandnumber"] = newNumber;

            trace.Trace($"Assigned parent command number: {newNumber}");

            
        }

        private void GenerateChildCommandNumber(IOrganizationService service, ITracingService trace, Entity caseEntity)
        {
            Guid parentCaseId = caseEntity.GetAttributeValue<EntityReference>("new_parentcaseid").Id;

            // Step 1: Locking Setup
            Guid lockEntityId = new Guid("5c8fee90-8821-e411-941e-001dd8b7210d"); // Replace this with actual Autonumber Settings GUID
            string lockKey = LockSettingsEntity(service, trace, lockEntityId);
            if (lockKey == null)
            {
                throw new InvalidPluginExecutionException("Could not acquire lock to generate unique child command number.");
            }

            try
            {
                // Step 2: Retrieve parent command number
                Entity parentCase = service.Retrieve("new_case", parentCaseId, new ColumnSet("new_parentcommandnumber"));
                int parentNumber = parentCase.GetAttributeValue<int>("new_parentcommandnumber");

                // Step 3: Get highest existing child command number
                QueryExpression query = new QueryExpression("new_case")
                {
                    ColumnSet = new ColumnSet("new_childcommandnumber"),
                    Criteria = new FilterExpression
                    {
                        Conditions = {
                    new ConditionExpression("new_parentcaseid", ConditionOperator.Equal, parentCaseId)
                }
                    },
                    Orders = {
                new OrderExpression("new_childcommandnumber", OrderType.Descending)
            }
                };

                EntityCollection children = service.RetrieveMultiple(query);

                int nextChildNum = (children.Entities.Count > 0)
                    ? children.Entities[0].GetAttributeValue<int>("new_childcommandnumber") + 1
                    : 1;

                string commandNum = parentNumber + "." + nextChildNum;

                caseEntity["new_childcommandnumber"] = nextChildNum;
                caseEntity["new_commandnumber"] = commandNum;
                caseEntity["new_parentcommandnumber"] = parentNumber;
                caseEntity["new_parentcaseid"] = new EntityReference("new_case", parentCaseId);

                trace.Trace("Assigned child command number: " + commandNum);

                // Step 4: Release Lock
                ReleaseSettingsEntityLock(service, lockEntityId, lockKey, nextChildNum);
            }
            catch (Exception ex)
            {
                trace.Trace("Error during GenerateChildCommandNumber: " + ex.ToString());
                throw;
            }
        }
        private string LockSettingsEntity(IOrganizationService service, ITracingService trace, Guid lockEntityId)
        {
            string lockKey = "L" + Guid.NewGuid().ToString();
            Entity lockEntity = new Entity("zt_autonumbersettings")
            {
                Id = lockEntityId
            };
            lockEntity.Attributes.Add("zt_lockkey", lockKey);

            int retries = 0;
            while (true)
            {
                if (retries > 50)
                {
                    trace.Trace("Failed to acquire lock after 50 attempts.");
                    return null;
                }

                try
                {
                    trace.Trace($"Attempting to lock with key: {lockKey}, Try: {retries}");
                    service.Update(lockEntity); // Attempt to lock
                    return lockKey;
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("already locked")) // Check if it's truly a lock conflict
                    {
                        throw;
                    }

                    trace.Trace($"Lock attempt failed (already locked). Retrying... Try: {retries}");
                    Thread.Sleep(500); // Wait 0.5 seconds before retrying
                    retries++;
                }
            }
        }

        private void ReleaseSettingsEntityLock(IOrganizationService service, Guid lockEntityId, string lockKey, int latestValue)
        {
            Entity unlockEntity = new Entity("zt_autonumbersettings")
            {
                Id = lockEntityId
            };

            unlockEntity["zt_secondarylockkey"] = lockKey;
            unlockEntity["zt_currentvalue"] = latestValue;

            service.Update(unlockEntity);
        }

    }

}
