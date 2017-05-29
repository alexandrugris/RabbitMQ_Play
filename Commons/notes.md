RabbitMQ
========

# General

Run the RabbitMQ docker container with the management console started: 

`docker run -d --rm --hostname my-rabbit -p 4369:4369 -p 15671-15672:15671-15672 -p 5672:5672 --name my_rabbit_mq rabbitmq:3-management` 

The corresponding connetion string is `"amqp://guest:guest@localhost:5672"` and the management URL: `http://localhost:15672/#/queues/%2F/alexandrugris.1st_queue`

# Basic concepts

Each service (application) maintains one connection to the queue. Connections are made to be shared across threads.

Within a connection, one or more channels can coexist to provide for concurrency. Rule of thumb: 1 channel / thread. Channels are not meant to be shared across threads. Connection objects is. Inside RabbitMQ, each channel is served by an Erlang thread (lightweight actor pattern, Erlang can spawn huge amount of threads).

Producers write to an exchange. Exchanges can communicate to queues or other exchanges through binding. Consumers read from queues. One service monitors one or more queues. Oldest message is consumed first.
Only when the queue receives the ACK, the message is deleted from the queue. Producers write to exchanges using a routing key. The exchange will route the message to the corresponding queue based on the routing key.

Inside the client, for receiving messages, one can set `prefetchCount` to load multiple messages. However, if the server crashes, these will remain unacknowledged even if processed:

```csharp
	if (cthread != System.Threading.Thread.CurrentThread.ManagedThreadId)
        throw new Exception("Channel reused from a different thread");

    chan.QueueDeclare(
             queue: Commons.Parameters.RabbitMQQueueName,
             durable: false,
             exclusive: false, 
             autoDelete: false, 
             arguments: null
             );

    chan.BasicQos(
              prefetchSize: 0, // no limit
              prefetchCount: 1, // 1 by 1
              global: false // true == set QoS for the whole connection or false only for this channel
              );

    chan.BasicConsume(Commons.Parameters.RabbitMQQueueName, noAck: false, consumer: this);
```

# Direct Exchange

Routing keys can have several terms separated by dots. E.g. `package.fast.international`. Queues listen to various keys by using wildcards. E.g. `package.*.international`. `*` is the wildcard for one word. `#` is the hashtag for multiple words.

# Fanout Exchange

The routing key is ignored. Message is sent to all bound queues.

# Question: 

Can rabbitmq be used as the infrastructure for a chat server in which each person is modelled as an actor? More precise, how many channels can a rabbitmq support?

The answer is yes, as the limit is not in the number of queues but in the number of TCP connections supported in a machine. For many connections, it is better to have a rabbitmq cluster. 

 - https://stackoverflow.com/questions/22989833/rabbitmq-how-many-queues-rabbitmq-can-handle-on-a-single-server
 - http://rabbitmq.1065348.n5.nabble.com/How-many-queues-can-one-broker-support-td21539.html
 - https://www.rabbitmq.com/distributed.html
 - RPC-like calls: http://www.rabbitmq.com/tutorials/tutorial-six-dotnet.html

# Microservices

As each microservice is persisting its data in ins own private database, with private indices, one needs a method for correlating various messages into a single logical entity. RabbitMQ provides a correlation ID property for the messange. A good value for it is a GUID.

 - http://jeftek.com/178/what-is-a-correlation-id-and-why-do-you-need-one/
 - https://stackoverflow.com/questions/20184755/practical-examples-of-how-correlation-id-is-used-in-messaging

# Reliability options

* Acks * - Rabbitmq only deletes a message from the queue when the message is acknowledged by the consumer. Can be set off in the consumer, which means a message is deleted as soon as it is delivered. Consumer is notified if a message is redelivered by a `redelivered == true` flag.

* Publisher confirms * - for the publisher to know that a message has been queued or not. Todo: implement a re-send strategy.

```csharp
	chan.ConfirmSelect();
	chan.BasicAcks += (o, args) => Console.WriteLine($"Msg confimed {args.DeliveryTag}");
	chan.BasicNacks += (o, args) => Console.WriteLine($"Error sending message to queue {args.DeliveryTag}");
```

* Mandatory * - set as a flag in `BasicPublish`. If the message cannot be routed to the queue it will be sent back to the producer. By default, if the flag is not set, the message is lost. The event `BasicReturn` is fired on the channel.

* Reply to sender * - producer is notified when the consumer has received the message. Use the `ReplyTo` field in message properties or use `SimpleRpcServer` and `SimpleRpcClient`. (https://www.rabbitmq.com/tutorials/tutorial-six-dotnet.html)

* Connection and topology recovery * - retry in case of failure to send messages

```csharp
var cf = new RabbitMQ.Client.ConnectionFactory
{
	Uri = Commons.Parameters.RabbitMQConnectionString,
	AutomaticRecoveryEnabled = true,
	TopologyRecoveryEnabled = true,
	NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
	UseBackgroundThreadsForIO = false // this is related to Thread.IsBackground property; Foreground threads keep the app alive until finished
};

conn = cf.CreateConnection();
```






