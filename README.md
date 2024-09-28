Lazvard Message is an AMQP server simulator that is **unofficially** compatible with Azure Service Bus. 

## Setup

### Docker

Create an empty folder and map it to the container for configuration storage. The container will generate a default config file upon its first run. After making any modifications, restart the container.

```bash
docker run -p 5671:5671 -v ./config:/App/config pejmann/lazvard-message

podman run -p 5671:5671 -v ./config:/App/config pejmann/lazvard-message
```

### Manual Build

You need to have .NET 8 installed on your operating system to run this project. You can download it from [here](https://dotnet.microsoft.com/en-us/download).


Since this application is not signed, you may encounter issues running it. The simplest way to run the project is to clone and build it on your operating system:
```
git clone https://github.com/PejmanNik/lazvard-message.git
cd lazvard-message
dotnet run --project ./src/Lazvard.Message.Cli
```

Alternatively, you can download the latest version of Lazvard from the release page. At least on Windows, you will need to manually trust the application in Microsoft's SmartScreen upon the first run.

```
wget -O ./lazvard.zip https://github.com/PejmanNik/lazvard-message/releases/download/v0.3.0/win-x64.zip
```

The application will create a default config file if it's not found on the first run. This config file is in TOML format. Before running Lazvard, you need to define all the queues, topics, and subscriptions in the config file. 

The new Azure SDK supports HTTP transport for local emulators, so HTTPS is no longer required. However, if you want to use HTTPS, you can set UseHttps to true in the config file. If you enable UseHttps, the AMQP server will require a valid and trusted X.509 certificate (PFX - PKCS #12). On Windows and macOS, the application can create and trust certificates using dotnet dev-certs. However, on Linux, you will need to manually set the certificate as trusted.

It's important to note that Lazvard is stateless, meaning that once you close it, all messages and information will be lost.


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




