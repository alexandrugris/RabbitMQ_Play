using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassTransit_Commons
{
    static class Commons
    {
        public const string MassTransitURL = "rabbitmq://localhost/mass_transit_test_queue";
        public const string Username = "guest";
        public const string Password = "guest";
    }

    public interface IMessage
    {
        string Message { get; set; }
    }

    public interface IMessage2 : IMessage
    {
        string SecondMessage { get; set; }
    }
}
