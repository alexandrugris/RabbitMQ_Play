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
            var cf = new ConnectionFactory { Uri = Commons.Parameters.RabbitMQConnectionString };
            conn = cf.CreateConnection(); // one connection, one channel per Thread
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
        
        public void Produce()
        {
            using (var chan = conn.CreateModel()) // this is a little bit forced because the threads will be recycled
            {
                chan.ExchangeDeclare(
                    exchange:   Commons.Parameters.RabbitMQExchangeName,
                    type:       ExchangeType.Direct,
                    durable:    false, // no serialization
                    autoDelete: false,
                    arguments:  null
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

                chan.BasicPublish(Commons.Parameters.RabbitMQExchangeName, "", msgProps, Encoding.UTF8.GetBytes(msg));

            }
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