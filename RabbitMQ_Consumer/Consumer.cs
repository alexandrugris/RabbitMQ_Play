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
        
        public Consumer() : base()
        {
            var cf = new ConnectionFactory { Uri = Commons.Parameters.RabbitMQConnectionString };
            var conn = cf.CreateConnection();

            chan = conn.CreateModel();

            conn.AutoClose = true; // auto-close the connection when there are no more channels
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            if (properties.ContentType != "application/json")
                throw new ArgumentException("We handle only json messages");

            try
            {
                var msg = JsonConvert.DeserializeObject<Commons.Message>(Encoding.UTF8.GetString(body));

                Console.WriteLine($"Message: {msg.Msg} from thread: {msg.ThreadID}, version: {msg.Version}, version_two_field: {msg.FieldAddedInVersion2}");

                chan.BasicAck(deliveryTag, false); // send ack only for this message and only if no error so far

            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.ToString());
                throw e; // throw further
            }            
        }
        
        public void Consume()
        {
            if (cthread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new Exception("Channel reused from a different thread");

            chan.QueueDeclare(
                    queue: Commons.Parameters.RabbitMQQueueName,
                    durable: false,
                    exclusive: false, // what does this mean?
                    autoDelete: false, // when does it autodelete?
                    arguments: null
                    );

            chan.BasicQos(
                prefetchSize: 0, // no limit
                prefetchCount: 1, // 1 by 1
                global: false // true == set QoS for the whole connection or false only for this channel
                );

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

            }
        }
    }
}