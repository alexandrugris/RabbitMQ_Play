using System;

namespace Commons
{
    public class Parameters
    {
        public static string RabbitMQConnectionString = "amqp://guest:guest@localhost:5672";
        public static string RabbitMQExchangeName  = "alexandrugris.1st_exchange";
        public static string RabbitMQQueueName = "alexandrugris.1st_queue";
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
