using System;

using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using Commons;

namespace RabbitMQ_Producer
{
    static class MyModelConfig
    {
        public static IModel ChanConfig(this IModel chan)
        {
            // now we also declare a dead letter exchange for the messages that were not processed
            chan.ExchangeDeclare(
                Commons.Parameters.RabbitMQExchangeName_DLX,
                ExchangeType.Fanout,
                durable: false,
                autoDelete: false,
                arguments: null
            );

            // to simplify the topology,
            // we will use the same dead letter exchange as alternative exchange in case of routing failures
            chan.ExchangeDeclare(
                    exchange: Commons.Parameters.RabbitMQExchangeName,
                    type: ExchangeType.Direct, // change to Fanout to send to several queues
                    durable: false, // no serialization
                    autoDelete: false,
                    arguments: new Dictionary<string, object>()
                    {
                        { "alternate-exchange", Commons.Parameters.RabbitMQExchangeName_DLX }
                    }
             );
            
            chan.QueueDeclare(
                queue: Commons.Parameters.RabbitMQQueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object>()
                {
                    { "x-dead-letter-exchange", Commons.Parameters.RabbitMQExchangeName_DLX }
                }
            );

            chan.QueueDeclare(
                queue: Commons.Parameters.RabbitMQQueueName_DLX,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            chan.QueueBind(
                queue: Commons.Parameters.RabbitMQQueueName,
                exchange: Commons.Parameters.RabbitMQExchangeName,
                routingKey: "RabbitMQ_Play");

            /**
             * The dead-lettering process adds an array to the header of each dead-lettered message named x - death.
             * This array contains an entry for each dead lettering event, identified by a pair of { queue, reason}. 
             * https://www.rabbitmq.com/dlx.html
             */

            chan.QueueBind(
                queue: Commons.Parameters.RabbitMQQueueName_DLX,
                exchange: Commons.Parameters.RabbitMQExchangeName_DLX,
                routingKey: ""
            );

            // for publisher to get confirmation that the message has been received by the queue: 
            chan.ConfirmSelect();
            chan.BasicAcks += (o, args) =>  Console.WriteLine($"Msg confimed {args.DeliveryTag}"); 
            chan.BasicNacks += (o, args) => Console.WriteLine($"Error sending message to queue {args.DeliveryTag}");

            
            return chan;
        }
    }

    class MultithreadedProducer : IDisposable
    {        
        private IConnection conn = null;
        private ConcurrentDictionary<int, IModel> openChannels = new ConcurrentDictionary<int, IModel>();

        int msg_id = -1;

        MultithreadedProducer()
        {
            conn = Commons.Parameters.RabbitMQConnection;
        }

        #region IDisposable Support
        private bool disposed = false; // To detect redundant calls       

        ~MultithreadedProducer()
        {
            try
            {
                if (!disposed)
                    throw new Exception("use Dispose!");
            }
            finally
            {
                if (!disposed)
                    Dispose();
            }
        }
        
        public void Dispose()
        {
            if (disposed)
                return;

            if (conn.IsOpen)
            {
                foreach (var chan in openChannels.Values) {
                    chan.Close();
                    chan.Dispose();
                }

                openChannels.Clear();
                conn.Close();
            }
        }
        #endregion

        public void Produce()
        {
            var chan = openChannels.GetOrAdd(Thread.CurrentThread.ManagedThreadId, (thid) => conn.CreateModel().ChanConfig());
            ChanSendMessage(chan);
        }

        /// <summary>
        /// Together with the dead letter queue can be used to implement the scheduled delivery pattern
        /// </summary>
        /// <param name="when"></param>
        /// <returns></returns>
        private string GetExpirationAtTimeMillisAsString(DateTime when)
        {
            double millis = (when - DateTime.Now).TotalMilliseconds;
            millis = (millis > 0) ? millis : TimeSpan.FromMinutes(1).TotalMilliseconds;
            return ((int)millis).ToString();
            
        }

        private void ChanSendMessage(IModel chan)
        {
            Interlocked.Increment(ref msg_id);

            var msg = JsonConvert.SerializeObject(new Commons.Message()
            {
                Msg = "Hello World",
                ThreadID = Thread.CurrentThread.ManagedThreadId,
                MsgID = msg_id,
                FieldAddedInVersion2 = "Hello World"
            });

            var msgProps = chan.CreateBasicProperties();
            msgProps.ContentType = "application/json";
            msgProps.CorrelationId = Guid.NewGuid().ToString(); // set a correlation id to the message

            // scheduled delivery pattern in conjunction with the dead letter queue
            msgProps.Expiration = GetExpirationAtTimeMillisAsString(DateTime.Today.AddHours(12)); // send messages today at noon

            // force some routing failures
            string routingKey = Parameters.RandomEvent && Parameters.RandomEvent ? "Garbage - to alternate exchange" : "RabbitMQ_Play";
            chan.BasicPublish(Commons.Parameters.RabbitMQExchangeName, routingKey, msgProps, Encoding.UTF8.GetBytes(msg));
        }

        static void Main(string[] args)
        {
            using (var producer = new MultithreadedProducer())
            {
                var tasks = new List<Task>();

                for (int i = 0; i < 20000; i++)
                {
                    tasks.Add(Task.Run(() => { producer.Produce(); }));
                }

                Task.WaitAll(tasks.ToArray());
                
            }
        }
    }

}