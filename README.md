# Create BizTalk360 Custom Notification Channel
One of the great new features of the BizTalk360 8.0 release is the possibility to add custom notification channels. This way we can send notifications to any target, like a ticketing system, or a central dashboard like Nagios. Setting up these notification channels is easy too, we just have to implement BizTalk360's IChannelNotification interface. In this sample we will send the notifications to a Azure Service Bus Topic, from where we can import it into our client application.

## Settings UI
We will start by creating the UI where the settings for connecting to our topic are specified. Settings can be set on two places. The first is the global settings of the notification channel, settings specified here will be used by all alarms using the notification channel. In this sample we will specify the name and authorization details of the Service Bus namespace. The second is the alarm settings, these will be used only by the specific alarm, and can be different on each alarm where the notification channel is used. We will specify the topic to which we want to send our messages here. To set up the settings UI we have to have two xml files. Let's start with GlobalProperties.xml.

```XML
<prop:GlobalProperties xmlns:bsd="http://www.biztalk360.com/alarms/notification/basetypes" 
                       xmlns="http://www.biztalk360.com/alarms/notification/basetypes" 
                       xmlns:prop="http://www.biztalk360.com/alarms/notification/properties"> 
  <Section Name="service-bus-namespace" DisplayName="Service Bus Namespace"> 
    <TextBox Name="service-bus-namespace-name" DisplayName="Service Bus Namespace Name" IsMandatory="true" Tooltip="Name of the Service Bus namespace where you want to send 
 
the notifications." DefaultValue="YourNamespace" Type="text"/> 
    <TextBox Name="service-bus-shared-access-key-name" DisplayName="Shared Access Key Name" IsMandatory="true" Tooltip="Name of the shared access key used to connect." 
 
DefaultValue="RootManageSharedAccessKey" Type="text"/> 
    <TextBox Name="service-bus-shared-access-key" DisplayName="Shared Access Key" IsMandatory="true" Tooltip="Shared access key used to connect." DefaultValue="" 
 
Type="text"/> 
  </Section> 
</prop:GlobalProperties>
```

We have specified three textboxes here for the global settings, including default values and tooltips which will be shown when hovered over the textbox. Next we will create AlarmProperties.xml, where we will spevify the alarm settings.

```XML
<prop:AlarmProperties xmlns="http://www.biztalk360.com/alarms/notification/basetypes" 
                      xmlns:prop="http://www.biztalk360.com/alarms/notification/properties" 
                      Name="servicebus-settings" 
                      DisplayName="Service Bus Settings"> 
  <TextBox Name="topic" DisplayName="Topic" IsMandatory="true" Type="text"/> 
</prop:AlarmProperties>
```

For both these files the build action should be set to Embedded Resource. To read the settings we will create the Helper class, which will retrieve the resource files and get their contents.

```C#
internal class Helper 
{ 
    /// <summary> 
    /// Get contents of the property files. 
    /// </summary> 
    internal static string GetResourceFileContent(string filename) 
    { 
        // Get assembly 
        var assembly = Assembly.GetExecutingAssembly(); 
 
        // Get assembly name 
        var name = assembly.GetName().Name; 
 
        // Get property file from the assembly resources 
        using (var stream = assembly.GetManifestResourceStream(name + "." + filename)) 
        { 
            // Check if property file could be found 
            if (stream == null) 
            { 
                throw new Exception(string.Format("Cannot read {0} make sure the file exists and it's an embeded resource", filename)); 
            } 
 
            // Read contents from the property file 
            using (var streamReader = new StreamReader(stream)) 
            { 
                return streamReader.ReadToEnd(); 
            } 
        } 
    } 
}
```

## Process Alert
Next we will create our ServiceBusNotification class, which will implement the IChannelNotification interface, which can be found in B360.Notifier.Common. The DLL can be found in the <Program Files>\Kovai Ltd\BizTalk360\Service directory. Because we want to send to Azure Service Bus, we will have to add the Microsoft Azure Service Bus NuGet package and reference the [Microsoft.ServiceBus.Messaging](https://msdn.microsoft.com/en-US/library/Microsoft.ServiceBus.Messaging.aspx) namespace. Do not forget to add the DLL's (Microsoft.ServiceBus.dll and Microsoft.WindowsAzure.Configuration.dll) to the GAC on the BizTalk360 server, or our notification channel will not work. This should be done for any libraries we use in our notification channels. In our class we will start by adding the methods which will retrieve our settings from the xml's we created earlier, using the Helper class.

```C#
public string GetGlobalPropertiesSchema() 
{ 
    return Helper.GetResourceFileContent("GlobalProperties.xml"); 
} 
 
public string GetAlarmPropertiesSchema() 
{ 
    return Helper.GetResourceFileContent("AlarmProperties.xml"); 
}
```

Finally we will add the SendNotification method, which receives the alarm and environment properties from BizTalk360. We will use these along with the BT360Helper class to get the details of the alert. Here we will create a BrokeredMessage which is then sent to the Service Bus Topic, setting properties for routing to various subscriptions.

```C#
public bool SendNotification(BizTalkEnvironment environment, Alarm alarm, string globalProperties, Dictionary<MonitorGroupTypeName, MonitorGroupData> notifications) 
{ 
    try 
    { 
        // Read global properties 
        var global = XDocument.Parse(globalProperties); 
        var namespaceName = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 'service-bus- 
 
namespace-name']").Attribute("Value").Value; 
        var sharedAccessKeyName = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 
 
'service-bus-shared-access-key-name']").Attribute("Value").Value; 
        var sharedAccessKey = global.XPathSelectElement("/*[local-name() = 'GlobalProperties']/*[local-name() = 'Section']/*[local-name() = 'TextBox' and @Name = 'service- 
 
bus-shared-access-key']").Attribute("Value").Value; 
 
        // Read alarm properties 
        var alarmProperties = XDocument.Parse(alarm.AlarmProperties); 
        var topic = alarmProperties.XPathSelectElement("/*[local-name() = 'AlarmProperties']/*[local-name() = 'TextBox' and @Name = 'topic']").Attribute("Value").Value; 
 
        // Construct message 
        var message = new StringBuilder(); 
        message.AppendFormat("\nAlarm Name: {0} \n\nAlarm Desc: {1} \n", alarm.Name, alarm.Description); 
        message.Append("\n----------------------------------------------------------------------------------------------------\n"); 
        message.AppendFormat("\nEnvironment Name: {0} \n\nMgmt Sql Instance Name: {1} \nMgmt Sql Db Name: {2}\n", environment.Name, environment.MgmtSqlDbName, 
 
environment.MgmtSqlInstanceName); 
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
```

## Installation
Now that we have created our custom notification channel we can install it into our BizTalk360. Open the settings in BizTalk360 go to Monitoring and Notification and choose Manage Notification Channels. Create a new notification and set the name, optionally add a Logo, and choose the DLL of the solution we just created.

![](https://code.msdn.microsoft.com/site/view/file/148603/1/Untitled.png)

Click Validate and Render UI to check if the UI is as expected, and save it if everything is correct. We now have to set our global properties, which will be used throughout all instances of this notification channel.

![](https://code.msdn.microsoft.com/site/view/file/148604/1/2.png)

Now that our channel has been created, we can add it to our alarms. Either create a new alarm, or edit an existing alarm, and go to the Advanced tab. Here we will find all enabled notification channels. Enable the channel which we just created, and set the topic to which our messages should be sent.

![](https://code.msdn.microsoft.com/site/view/file/148605/1/4.png)

Now whenever BizTalk360 sends out a notification, it will also be sent to our topic, which we can use to feed into other applications. Of course you can also use this to send them to other targets, and as you have seen it only takes a few lines of code. 

![](https://code.msdn.microsoft.com/site/view/file/148606/1/5.png)

## Conclusion
I really love this feature myself, as BizTalk360 is a great tool for monitoring and administrating tool for BizTalk, but sometimes there is a need from operations for a single view for all applications in the company, which we now can easily integrate with.
