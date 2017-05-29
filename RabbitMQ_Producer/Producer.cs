using System;

using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.Threading;

namespace RabbitMQ_Producer
{
    class Producer : IDisposable
    {        
        private IConnection conn = null;

        Producer()
        {
            conn = Commons.Parameters.RabbitMQConnection;
        }

        #region IDisposable Support
        private bool disposed = false; // To detect redundant calls       

        ~Producer()
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

            if(conn.IsOpen)
                conn.Close(); // TODO: check with channels
        }
        #endregion

        int msg_id = -1;

        private void ChanConfig(IModel chan)
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
                exclusive: false, // what does this mean?
                autoDelete: false, // when does it autodelete?
                arguments: null
                );

            chan.QueueBind(
                queue: Commons.Parameters.RabbitMQQueueName,
                exchange: Commons.Parameters.RabbitMQExchangeName,
                routingKey: "");

            /// for publisher to get confirmation that the message has been received by the queue:

            
            chan.ConfirmSelect();
            chan.BasicAcks += (o, args) =>
            {
                bool closed = chan.IsClosed; // msg is received even after the channel is closed
                // https://stackoverflow.com/questions/20095049/rabbitmq-deliverytag-always-1 - why?
                Console.WriteLine($"Msg confimed {args.DeliveryTag}");
            };
            chan.BasicNacks += (o, args) => Console.WriteLine($"Error sending message to queue {args.DeliveryTag}");
        }
        

        public void Produce()
        {
            bool msg_sent = false;
            while (!msg_sent)
            {
                try
                {
                    using (var chan = conn.CreateModel()) // this is a little bit forced because the threads will be recycled
                    {

                        ChanConfig(chan);
                        ChanSendMessage(chan);
                        msg_sent = true;

                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
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
            using (var producer = new Producer())
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