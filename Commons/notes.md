RabbitMQ
========

1 service : 1 connection

within the connection, 1 or more channels (concurrency). Rule of thumb: 1 channel / thread. Channels are not meant to be shared across threads. Connection objects is.

Producers write to an exchange. Exchanges can communicate to queues or other exchanges through binding. Consumers read from queues. One service monitors one or more queues. Oldest message is consumed first.

Only when the queue receives the ACK, the message is deleted from the queue.

Producers write to exchanges using a routing key. The exchange will route the message to the corresponding queue based on the routing key.

Direct Exchange

Routing keys can have several terms separated by dots. E.g. "package.fast.international". Queues listen to various keys by using wildcards. E.g. "package.*.international". '*' is the wildcard for one word. '#' is the hashtag for multiple words.

Fanout Exchange

The routing key is ignored. Message is sent to all bound queues.

// for receiving messages, one can set prefetchCount to load multiple messages. However, if the server crashes, these will remain unacknowledged even if processed.

Question: can rabbitmq be used as the infrastructure for a chat server in which each person is modelled as an actor? more precise, how many channels can a rabbitmq support?

The answer is yes, as the limit is not in the number of queues but in the number of TCP connections supported in a machine. For many connections, it is better to have a rabbitmq cluster. 

https://stackoverflow.com/questions/22989833/rabbitmq-how-many-queues-rabbitmq-can-handle-on-a-single-server

http://rabbitmq.1065348.n5.nabble.com/How-many-queues-can-one-broker-support-td21539.html

https://www.rabbitmq.com/distributed.html

RPC-like calls: http://www.rabbitmq.com/tutorials/tutorial-six-dotnet.html
