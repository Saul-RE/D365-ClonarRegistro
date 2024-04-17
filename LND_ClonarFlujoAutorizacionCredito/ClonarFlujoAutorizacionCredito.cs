using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Web.DynamicData;
using System.Xml;

namespace LND_ClonarFlujoAutorizacionCredito
{
    public class ClonarFlujoAutorizacionCredito : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                ExecuteCloneRecord(service, tracingService, context);
            }
            catch (Exception ex)
            {
                tracingService.Trace("FollowUpPlugin: {0}", ex.ToString());

                throw new InvalidPluginExecutionException("An error occurred in FollowUpPlugin.", ex);
            }
        }

        protected void ExecuteCloneRecord(IOrganizationService service, ITracingService tracingService, IPluginExecutionContext context)
        {
            try
            {
                var target = ((EntityReference)context.InputParameters["inputRecord"]);
                var inputEntity = service.Retrieve(target.LogicalName, target.Id, new ColumnSet(true));
                var outputEntity = new Entity(inputEntity.LogicalName);

                outputEntity = ReplaceKey(inputEntity, outputEntity);
                Guid clonedEntGuid = service.Create(outputEntity);

                var outputEntityReference = new EntityReference(outputEntity.LogicalName, clonedEntGuid);

                tracingService.Trace("Create record sucessfull");
                context.OutputParameters["outputRecord"] = outputEntityReference;

                var inputEntityId = inputEntity.Id.ToString();
                Clone_children(service, inputEntityId, "lnd_aprobadordecredito", "lnd_autorizacioncredito", clonedEntGuid, inputEntity.LogicalName);

                //Obtener campos del aprobador del registro a clonar
                //String FetchXML = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                //        <entity name='lnd_aprobadordecredito'>
                //            <attribute name='lnd_aprobadordecreditoid'/>
                //            <attribute name='lnd_name'/>
                //            <attribute name='createdon'/>
                //            <attribute name='lnd_titulo'/>
                //            <attribute name='statuscode'/>
                //            <attribute name='statecode'/>
                //            <attribute name='lnd_correo'/>
                //            <attribute name='lnd_autorizacioncredito'/>
                //            <order attribute='lnd_name' descending='false'/>
                //            <filter type='and'>
                //                <condition attribute='lnd_autorizacioncredito' operator='eq' value = '{" + inputEntityId + @"}'/>
                //                <condition attribute='statuscode' operator='eq' value='1'/>
                //            </filter>
                //        </entity>
                //      </fetch>";

                ////Obtener a los aprobadores a clonar hacia el nuevo registro clonado
                //EntityCollection entityCollection = service.RetrieveMultiple(new FetchExpression(FetchXML));
                //if (entityCollection.Entities.Count > 0)
                //{
                //    foreach (Entity entity in entityCollection.Entities)
                //    {
                //        Entity outputChild = new Entity(entity.LogicalName);
                //        ReplaceKey(entity, outputChild);
                //        //Crear la referencia del aprobador clonado y el nuevo registro clonado del Flujo de Aprobadores
                //        outputChild.Attributes["lnd_autorizacioncredito"] = new EntityReference(outputChild.LogicalName, clonedEntGuid);
                //        //Crear aprobador
                //        Guid clonedEntGuid2 = service.Create(outputChild);
                //    }

                //}

            }
            catch (Exception ex)
            {
                tracingService.Trace("FollowUpPlugin: {0}", ex.ToString());

                throw new InvalidPluginExecutionException("An error occurred in FollowUpPlugin.", ex);
            }

        }

        private Entity ReplaceKey(Entity inputEntity, Entity outputEntity)
        {
            try
            {
                    //loop through each key in the passed in entity and assign value to the new entity
                    foreach (string key in inputEntity.Attributes.Keys)
                {
                    //Status Codes and State codes are ignored we want these to be set to default value 
                    if (key == "statuscode" || key == "statecode" || key == "createdon" || key == "lnd_paispropietario" || key == "lnd_ordenautorizador" || key == "lnd_autorizacioncredito")
                    {
                        continue;
                    }

                    //if (inputEntity.LogicalName == "lnd_autorizadorcredito")
                    //{
                    //    if (key == "lnd_name" || key == "lnd_titulo" || key == "transactioncurrencyid")
                    //    {
                    //        continue;
                    //    }
                    //}

                    //switch statement to handle most entity types note child related entitites are ignored
                    switch (inputEntity.Attributes[key].GetType().ToString())
                    {
                        case "Microsoft.Xrm.Sdk.EntityReference":
                            outputEntity.Attributes[key] = inputEntity.GetAttributeValue<EntityReference>(key);
                            break;
                        case "Microsoft.Xrm.Sdk.OptionSetValue":
                            outputEntity.Attributes[key] = inputEntity.GetAttributeValue<OptionSetValue>(key);
                            break;
                        case "Microsoft.Xrm.Sdk.Money":
                            outputEntity.Attributes[key] = new Money(inputEntity.GetAttributeValue<Money>(key).Value);
                            break;
                        case "System.Guid":
                            //Don't set this, this is the Id record and a new one will be generated automatically
                            break;
                        default:
                            outputEntity.Attributes[key] = inputEntity.Attributes[key];
                            break;
                    }
                }

                return outputEntity;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in FollowUpPlugin.", ex);
            }
        }

        private void Clone_children(IOrganizationService service, string inputEntityId, string EntityNew, string inputEntityFieldId, Guid clonedEntGuid, string inputEntity)
        {

            var query = new QueryExpression(EntityNew);
            query.Criteria.AddCondition(new ConditionExpression(inputEntityFieldId, ConditionOperator.Equal, inputEntityId));
            query.Criteria.AddCondition(new ConditionExpression("statuscode", ConditionOperator.Equal, 1));
            query.ColumnSet = new ColumnSet(true);
            var inputChildren = service.RetrieveMultiple(query);
            foreach (var inputChild in inputChildren.Entities)
            {
                var outputChild = new Entity(inputChild.LogicalName);
                ReplaceKey(inputChild, outputChild);
                outputChild.Attributes[inputEntityFieldId] = new EntityReference(inputEntity, clonedEntGuid);
                Guid clonedEntGuid2 = service.Create(outputChild);

            }
        }


    }
}
