using System;

using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

namespace RabbitMQ_Producer
{
    static class MyModelConfig
    {
        public static IModel ChanConfig(this IModel chan)
        {
            chan.ExchangeDeclare(
                    exchange: Commons.Parameters.RabbitMQExchangeName,
                    type: ExchangeType.Direct, // change to Fanout to send to several queues
                    durable: false, // no serialization
                    autoDelete: false,
                    arguments: null
                    );

            chan.QueueDeclare(
                queue: Commons.Parameters.RabbitMQQueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
                );

            chan.QueueBind(
                queue: Commons.Parameters.RabbitMQQueueName,
                exchange: Commons.Parameters.RabbitMQExchangeName,
                routingKey: "");

            /// for publisher to get confirmation that the message has been received by the queue:


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

        private void ChanSendMessage(IModel chan)
        {
            Interlocked.Increment(ref msg_id);

            var msg = JsonConvert.SerializeObject(new Commons.Message()
            {
                Msg = "Hello World",
                ThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId,
                MsgID = msg_id,
                FieldAddedInVersion2 = "Hello World"
            });

            var msgProps = chan.CreateBasicProperties();
            msgProps.ContentType = "application/json";
            msgProps.CorrelationId = Guid.NewGuid().ToString(); // set a correlation id to the message

            chan.BasicPublish(Commons.Parameters.RabbitMQExchangeName, "", msgProps, Encoding.UTF8.GetBytes(msg));
        }

        static void Main(string[] args)
        {
            using (var producer = new MultithreadedProducer())
            {
                var tasks = new List<Task>();

                for (int i = 0; i < 10000; i++)
                {
                    tasks.Add(Task.Run(() => { producer.Produce(); }));
                }

                Task.WaitAll(tasks.ToArray());
                
            }
        }
    }

}