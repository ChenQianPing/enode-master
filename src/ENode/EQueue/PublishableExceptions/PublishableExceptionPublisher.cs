﻿using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.IO;
using ECommon.Serializing;
using ENode.Infrastructure;
using EQueue.Clients.Producers;
using EQueueMessage = EQueue.Protocols.Message;

namespace ENode.EQueue
{
    public class PublishableExceptionPublisher : IMessagePublisher<IPublishableException>
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ITopicProvider<IPublishableException> _exceptionTopicProvider;
        private readonly ITypeNameProvider _typeNameProvider;
        private readonly Producer _producer;
        private readonly SendQueueMessageService _sendMessageService;

        public Producer Producer { get { return _producer; } }

        public PublishableExceptionPublisher(ProducerSetting setting = null)
        {
            _producer = new Producer(setting);
            _jsonSerializer = ObjectContainer.Resolve<IJsonSerializer>();
            _exceptionTopicProvider = ObjectContainer.Resolve<ITopicProvider<IPublishableException>>();
            _typeNameProvider = ObjectContainer.Resolve<ITypeNameProvider>();
            _sendMessageService = new SendQueueMessageService();
        }

        public PublishableExceptionPublisher Start()
        {
            _producer.Start();
            return this;
        }
        public PublishableExceptionPublisher Shutdown()
        {
            _producer.Shutdown();
            return this;
        }
        public Task<AsyncTaskResult> PublishAsync(IPublishableException exception)
        {
            var message = CreateEQueueMessage(exception);
            return _sendMessageService.SendMessageAsync(_producer, message, exception.GetRoutingKey() ?? exception.Id);
        }

        private EQueueMessage CreateEQueueMessage(IPublishableException exception)
        {
            var topic = _exceptionTopicProvider.GetTopic(exception);
            var serializableInfo = new Dictionary<string, string>();
            exception.SerializeTo(serializableInfo);
            var sequenceMessage = exception as ISequenceMessage;
            var data = _jsonSerializer.Serialize(new PublishableExceptionMessage
            {
                UniqueId = exception.Id,
                AggregateRootTypeName = sequenceMessage != null ? sequenceMessage.AggregateRootTypeName : null,
                AggregateRootId = sequenceMessage != null ? sequenceMessage.AggregateRootStringId : null,
                Timestamp = exception.Timestamp,
                SerializableInfo = serializableInfo
            });
            return new EQueueMessage(
                topic,
                (int)EQueueMessageTypeCode.ExceptionMessage,
                Encoding.UTF8.GetBytes(data),
                _typeNameProvider.GetTypeName(exception.GetType()));
        }
    }
}
