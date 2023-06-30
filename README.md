Lazvard Message is an AMQP server simulator that is **unofficially** compatible with Azure Service Bus. 

## Setup

Download the last version of the Lazvard from release page or clone and build the project, the application will create the default config file if couldn't find it on the first run.

The config file is in TOML format. Before running Lazvard, you need to define all the queues, topics, and subscriptions in the config file. The AMQP server require a valid and trusted X.509 certificate (PFX - PKCS #12). In Windows and macOS, the application can create and trust a certificate (using a copy of the Microsoft .NET CLI - certificate manager). However, for Linux, you will need to manually set the certificate as trusted.

It's important to note that Lazvard is stateless. Therefore, once you close it, all messages and information will be lost.


## Different behavior

In addition to the standard AMQP protocol, the simulator's behavior largely relies on reverse engineering the Azure Service Bus client library and test suite. As a result, it is possible to encounter varying behaviors between the simulator and the actual Service Bus. If you come across any inconsistency, please create an issue with a failed test case or at least provide a sample code illustrating the misbehavior.

## Not Included 

While the primary goal of this project is to simulate all Azure Service Bus behaviors and features, there are currently some features that are not included:

- Message Sessions (In progress ⛏️)
- Scheduled messages
- Transactions
- Duplicate detection
- Messages Expiration
- Topic filters and actions
- Autoforwarding


## Etymology

Lazvard (LAZH-vard) is an alternative pronunciation of lazuli in Persian, and lazuli refers to a vivid blue mineral from which the color Azure derives its name.

[Wikipedia](https://en.wikipedia.org/wiki/Azure_(color)): 
> The color azure ultimately takes its name from the intense blue mineral lapis lazuli. Lapis is the Latin word for "stone" and lāzulī is the genitive form of the Medieval Latin lāzulum, which is taken from the Arabic لازورد lāzaward, itself from the Persian لاژورد lāžaward, which is the name of the stone in Persian and also of a place where lapis lazuli was mined




