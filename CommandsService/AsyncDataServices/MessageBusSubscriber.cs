using System.Text;
using CommandsService.EventProcessing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CommandsService.AsyncDataServices
{
    public class MessageBusSubscriber : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IEventProcessor _eventProcessor;
        private IConnection _connection;
        private IChannel _channel;
        private string _queueName;

        public MessageBusSubscriber(IConfiguration configuration, IEventProcessor eventProcessor)
        {
            _configuration = configuration;
            _eventProcessor = eventProcessor;
        }

        private async Task InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory() { HostName = _configuration["RabbitMQHost"], Port = int.Parse(_configuration["RabbitMQPort"]) };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            _queueName = (await _channel.QueueDeclareAsync()).QueueName;
            await _channel.ExchangeDeclareAsync(exchange: "trigger", type: ExchangeType.Fanout);            
            await _channel.QueueBindAsync(queue: _queueName, exchange: "trigger", routingKey: "");

            Console.WriteLine("--> Listening on the Message Bus...");

            _connection.ConnectionShutdownAsync += RabbitMQ_ConnectionShutdown;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await InitializeRabbitMQ();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (ModuleHandle, ea) =>
            {
                Console.WriteLine("--> Event Received!");

                var body = ea.Body;
                var notificationMessage = Encoding.UTF8.GetString(body.ToArray());
                _eventProcessor.PropcessEvent(notificationMessage);

                await Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Console.WriteLine("--> Connection ShutDown");
        }

        public override void Dispose()
        {
            if(_channel.IsOpen)
            {
                _channel.CloseAsync();
                _connection.CloseAsync();
            }

            base.Dispose();
        }
    }
}