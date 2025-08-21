using NUnit.Framework;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class GrowthBookResponseParserTests
    {
        private GrowthBookResponseParser parser;

        [SetUp]
        public void Setup()
        {
            parser = new GrowthBookResponseParser();
        }

        [Test]
        public void ParseResponse_ShouldReturnNull_WhenInputIsNull()
        {
            var result = parser.ParseResponse(null);
            Assert.IsNull(result);
        }

        [Test]
        public void ParseResponse_ShouldReturnNull_WhenInputIsEmpty()
        {
            var result = parser.ParseResponse("");
            Assert.IsNull(result);
        }

        [Test]
        public void ParseResponse_ShouldReturnNull_WhenJsonIsInvalid()
        {
            var result = parser.ParseResponse("{ invalid json }");
            Assert.IsNull(result);
        }

        [Test]
        public void ParseResponse_ShouldParseValidResponseWithFeatures()
        {
            // Sample JSON response with all supported features
            var json = @"{
                ""status"": ""success"",
                ""features"": {
                    ""sdk_event_single_url"": {
                        ""defaultValue"": ""https://default.mixpanel.com"",
                        ""rules"": []
                    },
                    ""sdk_event_batch_url"": {
                        ""defaultValue"": ""https://default-batch.mixpanel.com"",
                        ""rules"": []
                    },
                    ""sdk_enable_ad_id"": {
                        ""defaultValue"": ""true"",
                        ""rules"": []
                    },
                    ""sdk_request_timeout_seconds"": {
                        ""defaultValue"": ""30"",
                        ""rules"": []
                    },
                    ""sdk_config"": {
                        ""defaultValue"": ""{}"",
                        ""rules"": []
                    }
                }
            }";

            var result = parser.ParseResponse(json);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Features);
            Assert.AreEqual(5, result.Features.Count);

            // Check specific features exist
            Assert.IsTrue(result.Features.ContainsKey("sdk_event_single_url"));
            Assert.IsTrue(result.Features.ContainsKey("sdk_event_batch_url"));
            Assert.IsTrue(result.Features.ContainsKey("sdk_enable_ad_id"));
            Assert.IsTrue(result.Features.ContainsKey("sdk_request_timeout_seconds"));
            Assert.IsTrue(result.Features.ContainsKey("sdk_config"));

            // Check default values
            Assert.AreEqual("https://default.mixpanel.com", result.Features["sdk_event_single_url"].DefaultValue);
            Assert.AreEqual("https://default-batch.mixpanel.com", result.Features["sdk_event_batch_url"].DefaultValue);
            Assert.AreEqual("true", result.Features["sdk_enable_ad_id"].DefaultValue);
            Assert.AreEqual("30", result.Features["sdk_request_timeout_seconds"].DefaultValue);
            Assert.AreEqual("{}", result.Features["sdk_config"].DefaultValue);
        }

        [Test]
        public void ParseResponse_ShouldHandleFeatureWithRules()
        {
            var json = @"{
                ""status"": ""success"",
                ""features"": {
                    ""sdk_event_single_url"": {
                        ""defaultValue"": ""https://default.mixpanel.com"",
                        ""rules"": [
                            {
                                ""id"": ""test-rule-1"",
                                ""force"": ""https://forced-url.com"",
                                ""condition"": {
                                    ""gamename"": {
                                        ""value"": ""Test Game""
                                    }
                                }
                            }
                        ]
                    }
                }
            }";

            var result = parser.ParseResponse(json);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Features.ContainsKey("sdk_event_single_url"));

            var feature = result.Features["sdk_event_single_url"];
            Assert.AreEqual("https://default.mixpanel.com", feature.DefaultValue);
            Assert.IsNotNull(feature.Rules);
            Assert.AreEqual(1, feature.Rules.Count);

            var rule = feature.Rules[0];
            Assert.AreEqual("test-rule-1", rule.Id);
            Assert.AreEqual("https://forced-url.com", rule.ForceValue);
            Assert.IsNotNull(rule.Condition);
            Assert.IsTrue(rule.Condition.Attributes.ContainsKey("gamename"));
        }

        [Test]
        public void ParseResponse_ShouldHandleComplexConditions()
        {
            var json = @"{
                ""status"": ""success"",
                ""features"": {
                    ""sdk_event_single_url"": {
                        ""defaultValue"": ""https://default.mixpanel.com"",
                        ""rules"": [
                            {
                                ""id"": ""complex-rule"",
                                ""force"": ""https://forced-url.com"",
                                ""condition"": {
                                    ""gamename"": {
                                        ""value"": ""Test Game""
                                    },
                                    ""gameid"": {
                                        ""in"": [""id1"", ""id2""]
                                    },
                                    ""studioid"": {
                                        ""gt"": ""100""
                                    }
                                }
                            }
                        ]
                    }
                }
            }";

            var result = parser.ParseResponse(json);

            Assert.IsNotNull(result);
            var feature = result.Features["sdk_event_single_url"];
            var rule = feature.Rules[0];
            var condition = rule.Condition;

            Assert.IsNotNull(condition);
            Assert.AreEqual(3, condition.Attributes.Count);

            // Check gamename condition
            Assert.IsTrue(condition.Attributes.ContainsKey("gamename"));
            Assert.AreEqual("Test Game", condition.Attributes["gamename"].SimpleValue);

            // Check gameid condition
            Assert.IsTrue(condition.Attributes.ContainsKey("gameid"));
            Assert.IsNotNull(condition.Attributes["gameid"].InArray);
            Assert.AreEqual(2, condition.Attributes["gameid"].InArray.Length);
            Assert.Contains("id1", condition.Attributes["gameid"].InArray);
            Assert.Contains("id2", condition.Attributes["gameid"].InArray);

            // Check studioid condition
            Assert.IsTrue(condition.Attributes.ContainsKey("studioid"));
            Assert.AreEqual("100", condition.Attributes["studioid"].GreaterThan);
        }
    }
}