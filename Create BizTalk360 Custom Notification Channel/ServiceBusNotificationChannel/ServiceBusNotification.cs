using B360.Notifier.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.ServiceBus.Messaging;

namespace ServiceBusNotificationChannel
{
    /// <summary>
    /// Send BizTalk360 notifications to Service Bus topic.
    /// </summary>
    public class ServiceBusNotification : IChannelNotification
    {
        /// <summary>
        /// Get the global properties.
        /// </summary>
        public string GetGlobalPropertiesSchema()
        {
            return Helper.GetResourceFileContent("GlobalProperties.xml");
        }

        /// <summary>
        /// Get the alarm specific properties.
        /// </summary>
        public string GetAlarmPropertiesSchema()
        {
            return Helper.GetResourceFileContent("AlarmProperties.xml");
        }

        /// <summary>
        /// Send notification to external target.
        /// </summary>
        public bool SendNotification(BizTalkEnvironment environment, Alarm alarm, string globalProperties, Dictionary<MonitorGroupTypeName, MonitorGroupData> notifications)
        {
            try
            {
                // Read global properties
                var global = XDocument.Parse(globalProperties);
                var namespaceName = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 'service-bus-namespace-name']").Attribute("Value").Value;
                var sharedAccessKeyName = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 'service-bus-shared-access-key-name']").Attribute("Value").Value;
                var sharedAccessKey = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 'service-bus-shared-access-key']").Attribute("Value").Value;

                // Read alarm properties
                var alarmProperties = XDocument.Parse(alarm.AlarmProperties);
                var topic = alarmProperties.XPathSelectElement("/*[local-name() = 'AlarmProperties']/*[local-name() = 'TextBox' and @Name = 'topic']").Attribute("Value").Value;

                // Construct message
                var message = new StringBuilder();
                message.AppendFormat("\nAlarm Name: {0} \n\nAlarm Desc: {1} \n", alarm.Name, alarm.Description);
                message.Append("\n----------------------------------------------------------------------------------------------------\n");
                message.AppendFormat("\nEnvironment Name: {0} \n\nMgmt Sql Instance Name: {1} \nMgmt Sql Db Name: {2}\n", environment.Name, environment.MgmtSqlDbName, environment.MgmtSqlInstanceName);
                message.Append("\n----------------------------------------------------------------------------------------------------\n");
                var helper = new BT360Helper(notifications, environment, alarm);
                message.Append(helper.GetNotificationMessage());

                // Create connection string
                var connectionString =
                    string.Format(
                        "Endpoint=sb://{0}.servicebus.windows.net/;SharedAccessKeyName={1};SharedAccessKey={2}",
                        namespaceName, sharedAccessKeyName, sharedAccessKey);

                // Create a new topic client
                var topicClient = TopicClient.CreateFromConnectionString(connectionString, topic);

                // Create message, passing the string message for the body
                var brokeredMessage = new BrokeredMessage(message.ToString());

                // Set additional properties for routing
                brokeredMessage.Properties["Environment"] = environment.Name;
                brokeredMessage.Properties["Alarm"] = alarm.Name;

                // Send to Service Bus topic
                topicClient.Send(brokeredMessage);

                return true;
            }
            catch (Exception ex)
            {
                LoggingHelper.Fatal(ex.StackTrace);
                LoggingHelper.Error(ex.Message);
                return false;
            }
        }
    }
}
