using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ_Consumer
{
    class SingleThreadedConsumer : DefaultBasicConsumer, IDisposable
    {
        private IModel chan = null;    
        private int cthread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly int MAX_RETRY_COUNT = 1;

        public SingleThreadedConsumer(IConnection conn) : base()
        {
            chan = conn.CreateModel();
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

            if (!Crash) {

                var msg = JsonConvert.DeserializeObject<Commons.Message>(Encoding.UTF8.GetString(body));
                // Console.WriteLine($"Message: {msg.Msg} from thread: {msg.ThreadID}, version: {msg.Version}");
                chan.BasicAck(deliveryTag, false); // send ack only for this message and only if no error so far
            }
            else // error condition
            {
                // re-queuing in controlled way. chan.BasicNack() just requeues, but it is impossible to keep count of the number of retries
                // the following strategy, however, will place the message at the end of the queue.
                // if we were to use chan.BasicNack, the message would have been just pushed back and retried immediately.

                if (redelivered || GetRetryCount(properties) < MAX_RETRY_COUNT)
                {
                    Requeue(consumerTag, deliveryTag, exchange, routingKey, properties, body);
                }
                else
                {
                    // first time simply put it back in the queue for another try
                    chan.BasicNack(deliveryTag, false, true);
                }
            }            
        }

        private int GetRetryCount(IBasicProperties properties)
        {
            return  (int?)properties.Headers?["Retries"] ?? MAX_RETRY_COUNT;
        }

        private void SetRetryCount(IBasicProperties properties, int retryCount)
        {
            properties.Headers = properties.Headers ?? new System.Collections.Generic.Dictionary<string, object>();
            properties.Headers["Retries"] = retryCount;
        }

        private void Requeue(string consumerTag, ulong deliveryTag, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            int retryCount = GetRetryCount(properties);

            Console.WriteLine($"Retry count: {retryCount}");

            if (retryCount > 0)
            {
                SetRetryCount(properties, --retryCount);
               
                chan.BasicPublish(exchange, routingKey, properties, body);
                chan.WaitForConfirmsOrDie(); // this is slow, but we need to make sure somehow the message reaches the queue back
                chan.BasicAck(deliveryTag, false);
            }
            else
            {
                chan.BasicNack(deliveryTag, false, false); // reject the message to dead letter queue.
            }
        }

        public void Consume()
        {
            if (cthread != Thread.CurrentThread.ManagedThreadId)
                throw new Exception("Channel reused from a different thread");

            // topology of created in the producer
            
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
            const int N = 10;

            using (var conn = Commons.Parameters.RabbitMQConnection) {

                var threads = new System.Collections.Generic.List<Thread>();

                var waitfor = Task.Run(() => {
                    Console.ReadKey();
                });

                for (int i = 0; i < N; i++) // running N consumers at the same time
                {
                    var th = new Thread(() =>
                    {
                        using (var consumer = new SingleThreadedConsumer(conn))
                        {
                            consumer.Consume();
                            waitfor.Wait();
                        }
                    });

                    threads.Add(th);
                    th.Start();
                }
                waitfor.Wait();

                foreach (var t in threads) t.Join();
                conn.Close();
            }
            
        }
        
        
    }
}