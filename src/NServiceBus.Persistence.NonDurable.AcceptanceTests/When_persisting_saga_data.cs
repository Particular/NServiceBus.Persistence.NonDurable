namespace NServiceBus.Persistence.NonDurable.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_persisting_saga_data : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_support_basic_member_types()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointThatHostsASaga>(
                    b => b.When(session => session.SendLocal(new StartSaga
                    {
                        CorrelationId = Guid.NewGuid()
                    })))
                .Done(c => c.SagaDataLoaded)
                .Run();

            Assert.That(context.LoadedSagaData, Is.Not.Null);

            CollectionAssert.AreEquivalent(StringArray, context.LoadedSagaData.StringArray);

            CollectionAssert.AreEquivalent(Collection, context.LoadedSagaData.Collection);

            CollectionAssert.AreEquivalent(List, context.LoadedSagaData.List);

            Assert.That(context.LoadedSagaData.IntStringDictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.IntStringDictionary[1], Is.EqualTo(IntStringDictionary[1]));
                Assert.That(context.LoadedSagaData.IntStringDictionary[2], Is.EqualTo(IntStringDictionary[2]));

                Assert.That(context.LoadedSagaData.IntStringIDictionary, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.IntStringIDictionary[1], Is.EqualTo(IntStringIDictionary[1]));
                Assert.That(context.LoadedSagaData.IntStringIDictionary[2], Is.EqualTo(IntStringIDictionary[2]));

                Assert.That(context.LoadedSagaData.StringStringDictionary, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.StringStringDictionary["1"], Is.EqualTo(StringStringDictionary["1"]));
                Assert.That(context.LoadedSagaData.StringStringDictionary["2"], Is.EqualTo(StringStringDictionary["2"]));

                Assert.That(context.LoadedSagaData.StringObjectDictionary, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj1"].Guid, Is.EqualTo(StringObjectDictionary["obj1"].Guid));
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj1"].Int, Is.EqualTo(StringObjectDictionary["obj1"].Int));
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj1"].String, Is.EqualTo(StringObjectDictionary["obj1"].String));
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj2"].Guid, Is.EqualTo(StringObjectDictionary["obj2"].Guid));
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj2"].Int, Is.EqualTo(StringObjectDictionary["obj2"].Int));
                Assert.That(context.LoadedSagaData.StringObjectDictionary["obj2"].String, Is.EqualTo(StringObjectDictionary["obj2"].String));

                Assert.That(context.LoadedSagaData.ReadOnlyDictionary, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.ReadOnlyDictionary["hello"], Is.EqualTo(ReadOnlyDictionary["hello"]));
                Assert.That(context.LoadedSagaData.ReadOnlyDictionary["world"], Is.EqualTo(ReadOnlyDictionary["world"]));

                Assert.That(context.LoadedSagaData.DateTimeLocal, Is.EqualTo(DateTimeLocal));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.DateTimeLocal.Kind, Is.EqualTo(DateTimeLocal.Kind));
                Assert.That(context.LoadedSagaData.DateTimeUnspecified, Is.EqualTo(DateTimeUnspecified));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.DateTimeUnspecified.Kind, Is.EqualTo(DateTimeUnspecified.Kind));
                Assert.That(context.LoadedSagaData.DateTimeUtc, Is.EqualTo(DateTimeUtc));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.DateTimeUtc.Kind, Is.EqualTo(DateTimeUtc.Kind));

                Assert.That(context.LoadedSagaData.DateTimeOffset, Is.EqualTo(DateTimeOffset));
            });
            Assert.Multiple(() =>
            {
                Assert.That(context.LoadedSagaData.DateTimeOffset.Offset, Is.EqualTo(DateTimeOffset.Offset));
                Assert.That(context.LoadedSagaData.DateTimeOffset.LocalDateTime, Is.EqualTo(DateTimeOffset.LocalDateTime));
            });
        }

        static string[] StringArray =
        {
            "a",
            "b",
            "c"
        };

        static Dictionary<int, string> IntStringDictionary = new Dictionary<int, string>
        {
            {1, "hello"},
            {2, "world"}
        };

        static Dictionary<string, string> StringStringDictionary = new Dictionary<string, string>
        {
            {"1", "hello"},
            {"2", "world"}
        };

        static IDictionary<int, string> IntStringIDictionary = new Dictionary<int, string>
        {
            {1, "hello"},
            {2, "interface world"}
        };

        static Dictionary<string, SamplePoco> StringObjectDictionary = new Dictionary<string, SamplePoco>
        {
            {
                "obj1", new SamplePoco
                {
                    Guid = Guid.NewGuid(),
                    Int = 21,
                    String = "abc"
                }
            },
            {
                "obj2", new SamplePoco
                {
                    Guid = Guid.NewGuid(),
                    Int = 42,
                    String = "xyz"
                }
            }
        };

        static ICollection<string> Collection =
        [
            "1",
            "2"
        ];

        static IList<string> List =
        [
            "1",
            "2"
        ];

        static IReadOnlyDictionary<string, int> ReadOnlyDictionary = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>
        {
            {"hello", 11},
            {"world", 22}
        });

        static DateTime DateTimeLocal = new DateTime(2010, 10, 10, 10, 10, 10, DateTimeKind.Local);
        static DateTime DateTimeUnspecified = new DateTime(2010, 10, 10, 10, 10, 10, DateTimeKind.Unspecified);
        static DateTime DateTimeUtc = new DateTime(2010, 10, 10, 10, 10, 10, DateTimeKind.Utc);

        static DateTimeOffset DateTimeOffset = new DateTimeOffset(2010, 10, 10, 10, 10, 10, TimeSpan.FromHours(10));

        public class SamplePoco
        {
            public int Int { get; set; }
            public string String { get; set; }
            public Guid Guid { get; set; }
        }

        class Context : ScenarioContext
        {
            public SupportedFieldTypesSagaData LoadedSagaData { get; set; }
            public bool SagaDataLoaded { get; set; }
        }

        class EndpointThatHostsASaga : EndpointConfigurationBuilder
        {
            public EndpointThatHostsASaga() => EndpointSetup<DefaultServer>();

            public class SupportedFieldTypesSaga : Saga<SupportedFieldTypesSagaData>,
                IAmStartedByMessages<StartSaga>,
                IHandleMessages<LoadTheSagaAgain>
            {
                public SupportedFieldTypesSaga(Context context) => testContext = context;

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    Data.StringArray = StringArray;
                    Data.Collection = Collection;
                    Data.List = List;
                    Data.IntStringDictionary = IntStringDictionary;
                    Data.IntStringIDictionary = IntStringIDictionary;
                    Data.StringStringDictionary = StringStringDictionary;
                    Data.StringObjectDictionary = StringObjectDictionary;
                    Data.ReadOnlyDictionary = ReadOnlyDictionary;
                    Data.DateTimeLocal = DateTimeLocal;
                    Data.DateTimeUnspecified = DateTimeUnspecified;
                    Data.DateTimeUtc = DateTimeUtc;
                    Data.DateTimeOffset = DateTimeOffset;

                    return context.SendLocal(new LoadTheSagaAgain
                    {
                        DataId = Data.CorrelationId
                    });
                }

                public Task Handle(LoadTheSagaAgain message, IMessageHandlerContext context)
                {
                    testContext.LoadedSagaData = Data;
                    testContext.SagaDataLoaded = true;

                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SupportedFieldTypesSagaData> mapper) =>
                    mapper.MapSaga(saga => saga.CorrelationId)
                        .ToMessage<StartSaga>(m => m.CorrelationId)
                        .ToMessage<LoadTheSagaAgain>(m => m.DataId);

                Context testContext;
            }
        }

        public class SupportedFieldTypesSagaData : ContainSagaData
        {
            public Guid CorrelationId { get; set; }
            public string[] StringArray { get; set; }
            public Dictionary<int, string> IntStringDictionary { get; set; }
            public IDictionary<int, string> IntStringIDictionary { get; set; }
            public IDictionary<string, string> StringStringDictionary { get; set; }
            public Dictionary<string, SamplePoco> StringObjectDictionary { get; set; }
            public IReadOnlyDictionary<string, int> ReadOnlyDictionary { get; set; }
            public DateTime DateTimeLocal { get; set; }
            public DateTime DateTimeUnspecified { get; set; }
            public DateTime DateTimeUtc { get; set; }
            public DateTimeOffset DateTimeOffset { get; set; }
            public ICollection<string> Collection { get; set; }
            public IList<string> List { get; set; }
        }

        public class StartSaga : IMessage
        {
            public Guid CorrelationId { get; set; }
        }

        public class LoadTheSagaAgain : IMessage
        {
            public Guid DataId { get; set; }
        }
    }
}