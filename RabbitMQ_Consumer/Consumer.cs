using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Text;

namespace RabbitMQ_Consumer
{
    class Consumer : DefaultBasicConsumer, IDisposable
    {
        private IModel chan = null;    
        private int cthread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly int MAX_RETRY_COUNT = 5;

        public Consumer() : base()
        {
            var conn = Commons.Parameters.RabbitMQConnection;

            chan = conn.CreateModel();

            conn.AutoClose = true; // auto-close the connection when there are no more channels
        }

        static Random rnd = new Random();

        bool Crash
        {
            get
            {
                return rnd.Next() % 10 == 0;
            }
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            if (properties.ContentType != "application/json")
                throw new ArgumentException("We handle only json messages");

            try
            {
                var msg = JsonConvert.DeserializeObject<Commons.Message>(Encoding.UTF8.GetString(body));

                if (Crash)
                    throw new Exception($"Controlled crash: {deliveryTag}");

                // Console.WriteLine($"Message: {msg.Msg} from thread: {msg.ThreadID}, version: {msg.Version}");

                chan.BasicAck(deliveryTag, false); // send ack only for this message and only if no error so far
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.Message);
                // re-queuing in controlled way. chan.BasicNack() just requeues, but it is impossible to keep count of the number of retries
                // the following strategy, however, will place the message at the end of the queue.
                // if we were to use chan.BasicNack, the message would have been just pushed back and retried immediately.

                Requeue(consumerTag, deliveryTag, exchange, routingKey, properties, body);
            }            
        }

        private void Requeue(string consumerTag, ulong deliveryTag, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            int retryCount = (int?)properties.Headers?["Retries"] ?? MAX_RETRY_COUNT;

            if (retryCount > 0)
            {
                properties.Headers = properties.Headers ?? new System.Collections.Generic.Dictionary<string, object>();
                properties.Headers["Retries"] = --retryCount;

                chan.BasicPublish(exchange, routingKey, properties, body);
                chan.WaitForConfirmsOrDie(); // this is slow, but we need to make sure somehow the message reaches the queue back

            }
            // ack this message; 
            chan.BasicAck(deliveryTag, false);
        }

        public void Consume()
        {
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

            chan.ConfirmSelect(); // if a message is republished, just make sure that it reaches the queue and is not lost

            chan.BasicConsume(Commons.Parameters.RabbitMQQueueName, noAck: false, consumer: this);

        }
        
        public void Dispose()
        {           
            chan?.Dispose();
            chan = null;            
        }

        static void Main(string[] args)
        {
            using (var consumer = new Consumer())
            {
                consumer.Consume();
                System.Console.ReadKey();                
                // another way to go is to use the QueuingBasicConsumer(model) and then (BasicDeliveryEventArgs)consumer.Queue.Dequeue(); for extracting the message
            }
        }
    }
}