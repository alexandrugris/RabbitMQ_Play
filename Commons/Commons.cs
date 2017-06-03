using System;

namespace Commons
{
    public static class Parameters
    {
        public const string RabbitMQConnectionString = "amqp://guest:guest@localhost:5672";
        public const string RabbitMQExchangeName  = "alexandrugris.1st_exchange";
        public const string RabbitMQExchangeName_DLX = RabbitMQExchangeName + "_dead_letter_exchange";
        public const string RabbitMQQueueName = "alexandrugris.1st_queue";
        public const string RabbitMQQueueName_DLX = RabbitMQQueueName + "_dead_letter_exchange";

        /// <summary>
        /// TODO
        /// </summary>
        public static RabbitMQ.Client.IConnection RabbitMQConnection
        {
            get
            {  
                if (conn == null || !conn.IsOpen)
                {
                    var cf = new RabbitMQ.Client.ConnectionFactory
                    {
                        Uri = Commons.Parameters.RabbitMQConnectionString,
                        AutomaticRecoveryEnabled = true,
                        TopologyRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                        UseBackgroundThreadsForIO = false // this is related to Thread.IsBackground property; Foreground threads keep the app alive until finished
                    };

                    conn = cf.CreateConnection();
                }

                return conn;
            }
        }

        private static RabbitMQ.Client.IConnection conn = null;

    }

    public class Message
    {
        public int      Version = 2;
        public string   Msg = null;
        public int      ThreadID = -1;
        public int      MsgID = -1;
        public string   FieldAddedInVersion2 = null;
    }
}
