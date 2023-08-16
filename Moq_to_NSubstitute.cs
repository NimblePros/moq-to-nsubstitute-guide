// ============================
// Moving from Moq to NSubstitute
// ============================
// I'll share this document in Discord!

// ============================
// Setting up a mock return
// ============================

// public Widget Create()
_mock.Setup(factory => factory.Create()).Returns(new Widget());
_mock.Create().Returns(new Widget());

// public Task<string> GetAsync()
_mock.Setup(client => client.GetAsync()).ReturnsAsync("Foo");
_mock.GetAsync().Returns("Foo");

// ============================
// Verifying calls
// ============================
_mock.Verify(factory => factory.Create());
_mock.Received().Create();

_mock.Verify(factory => factory.Create(), Times.Once);
_mock.Received(1).Create();

_mock.Verify(factory => factory.Create(), Times.Never);
_mock.Received(0).Create();

_mock.Verify(factory => factory.Create(), Times.Exactly(5));
_mock.Received(5).Create();

_mock.VerifyNoOtherCalls();
// There is no equivalent to VerifyNoOtherCalls ( https://stackoverflow.com/a/65866073 -- dev )

// ============================
// Verifying asynchronous calls
// ============================
_mock.Verify(client => client.GetAsync());
await _mock.Received().GetAsync(); // need to await in NSubstitute

// ============================
// Setting up arguments in mocked method calls
// ============================
_mock.Setup(calc => calc.Add(It.IsAny<int>()));
_mock.Setup(calc => calc.Add(It.Is<int>(num => num % 2 == 0));

_mock.Add(Arg.Any<int>()).Returns(5);
_mock.Add(Arg.Is<int>(num => num % 2 == 0)).Returns(5);

// ============================
// Setting up exceptions
// ============================
_mock.Setup(factory => factory.Create()).Throws(new Exception());
_mock.Create().Throws(new Exception());

_mock.Setup(client => client.GetAsync()).ThrowsAsync(new Exception());
_mock.GetAsync().ThrowsAsync(new Exception());

// ============================
// Referencing passed-in arguments in what's returned
// ============================

// public int Add(int numberToAdd);
// Moq (separate typed parameters)
_mock.Setup(calc => calc.Add(1)).Returns((int numberToAdd) => 5 + numberToAdd);
_mock.Setup(calc => calc.Add(1)).Returns<int>(numberToAdd => 5 + numberToAdd);
// NSubstitute "args" is object[]
_mock.Add(1).Returns(callInfo => 5 + (int)callInfo.Args()[0]);
_mock.Add(1).Returns(callInfo => 5 + callInfo.Arg<int>()); // Don't need index if type is unique among arguments.
_mock.Add(1).Returns(callInfo => 5 + callInfo.ArgAt<int>(0)); // Specify which argument.

// ============================
// Callbacks
// ============================
// Moq
_mock.Setup(calc => calc.Add(1)).Callback((int numberToAdd) => { /* Called when mocked method invoked */ });
_mock.Setup(calc => calc.Add(1)).Callback<int>(numberToAdd => { /* Called when mocked method invoked */ });
// NSubstitute
_mock.Add(1).Returns(args => { /* Called when mocked method invoked. Same as referencing passed-in arguments */ });

// ============================
// Access a mock's calls/arguments
// ============================
_mock.Invocations();
_mock.Invocations[0].Arguments;

_mock.ReceivedCalls();
_mock.ReceivedCalls().First().GetArguments();

// ============================
// Mocking Non-Public abstract methods (short answer: Be careful!)
// ============================
// Example mocking HttpMessageHandler, used when needing to mock HttpClient

// Moq, with 'using Moq.Protected;'
mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
    ItExpr.IsAny<HttpRequestMessage>(),
    ItExpr.IsAny<CancellationToken>()
  ).ReturnsAsync(response);

// NSubstitute ( https://stackoverflow.com/a/62174168 )
Task<HttpResponseMessage> invocation = (Task<HttpResponseMessage>)_mockHandler
  .GetType().GetMethod("SendAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
// NOTE: NSubstitute.Analyzers will yell at you for using Arg.Any in a context it doesn't recognize as correct... probably for good reason.
  .Invoke(_mockHandler, new object[] { Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>() })!;
// Have to cast invocation to Task<HttpResponseMessage>, otherwise Returns() doesn't work as expected.
// Still a work-in-progress, as I've had issues attempting these kinds of mock setups.
invocation.Returns(new HttpResponseMessage(HttpStatusCode.OK));

// ============================
// NSubstitute and abusing Arg.Any
// ============================
// Only use in mocking contexts. If you don't, you can get weird stuff!
// Doing this in Moq mostly "just worked" / didn't yell at you.
// Especially when running lots of tests in parallel (race conditions)
//    AmbiguousArgumentsException
//    RedundantArgumentMatcherException

// From NSubstitute exception failure output:
var sub = Substitute.For<SomeClass>();
var realType = new MyRealType(sub);
// INCORRECT, arg spec used on realType, not a substitute:
realType.SomeMethod(Arg.Any<int>()).Returns(2);
// INCORRECT, arg spec used as a return value, not to specify a call:
sub.VirtualMethod(2).Returns(Arg.Any<int>());
// INCORRECT, arg spec used with a non-virtual method:
sub.NonVirtualMethod(Arg.Any<int>()).Returns(2);
// CORRECT, arg spec used to specify virtual call on a substitute:
sub.VirtualMethod(Arg.Any<int>()).Returns(2);


// Full build fail examples. The cause was a real class being called like this:
_realClass.Create(foo, Arg.Any<int>());


[xUnit.net 00:00:59.27]     UnitTests.HandlerTests.Handle.TestName [FAIL]
  Failed UnitTests.HandlerTests.Handle.TestName [10 ms]
  Error Message:
   NSubstitute.Exceptions.AmbiguousArgumentsException : Cannot determine argument specifications to use. Please use specifications for all arguments of the same type.
Method signature:
    SingleOrDefaultAsync(ISingleResultSpecification<Widget>, CancellationToken)
Method arguments (possible arg matchers are indicated with '*'):
    SingleOrDefaultAsync(*<null>*, *System.Threading.CancellationToken*)
All queued specifications:
    any Int32
    any Int32
    any Int32
    any Int32
    any ISingleResultSpecification<Widget>
    any CancellationToken
Matched argument specifications:
    SingleOrDefaultAsync(???, ???)

  Stack Trace:
     at NSubstitute.Core.Arguments.ArgumentSpecificationsFactory.Create(IList`1 argumentSpecs, Object[] arguments, IParameterInfo[] parameterInfos, MethodInfo methodInfo, MatchArgs matchArgs)
   at NSubstitute.Core.CallSpecificationFactory.CreateFrom(ICall call, MatchArgs matchArgs)
   at NSubstitute.Routing.Handlers.RecordCallSpecificationHandler.Handle(ICall call)
   at NSubstitute.Routing.Route.Handle(ICall call)
   at NSubstitute.Proxies.CastleDynamicProxy.CastleForwardingInterceptor.Intercept(IInvocation invocation)
   at Castle.DynamicProxy.AbstractInvocation.Proceed()
   at Castle.DynamicProxy.AbstractInvocation.Proceed()
   at Castle.Proxies.ObjectProxy_2.SingleOrDefaultAsync(ISingleResultSpecification`1 specification, CancellationToken cancellationToken)
   
   
   
===============


[xUnit.net 00:00:43.50]     UnitTests.HandlerTests.Handle.HandleTest [FAIL]
  Failed UnitTests.HandlerTests.Handle.HandleTest [3 ms]
  Error Message:
   NSubstitute.Exceptions.RedundantArgumentMatcherException : Some argument specifications (e.g. Arg.Is, Arg.Any) were left over after the last call.

This is often caused by using an argument spec with a call to a member NSubstitute does not handle (such as a non-virtual member or a call to an instance which is not a substitute), or for a purpose other than specifying a call (such as using an arg spec as a return value). For example:

    var sub = Substitute.For<SomeClass>();
    var realType = new MyRealType(sub);
    // INCORRECT, arg spec used on realType, not a substitute:
    realType.SomeMethod(Arg.Any<int>()).Returns(2);
    // INCORRECT, arg spec used as a return value, not to specify a call:
    sub.VirtualMethod(2).Returns(Arg.Any<int>());
    // INCORRECT, arg spec used with a non-virtual method:
    sub.NonVirtualMethod(Arg.Any<int>()).Returns(2);
    // CORRECT, arg spec used to specify virtual call on a substitute:
    sub.VirtualMethod(Arg.Any<int>()).Returns(2);

To fix this make sure you only use argument specifications with calls to substitutes. If your substitute is a class, make sure the member is virtual.

Another possible cause is that the argument spec type does not match the actual argument type, but code compiles due to an implicit cast. For example, Arg.Any<int>() was used, but Arg.Any<double>() was required.

NOTE: the cause of this exception can be in a previously executed test. Use the diagnostics below to see the types of any redundant arg specs, then work out where they are being created.

Diagnostic information:

Remaining (non-bound) argument specifications:
    any Int32
    any Int32
    any Int32
    any Int32

All argument specifications:
    any Int32
    any Int32
    any Int32
    any Int32

  Stack Trace:
     at NSubstitute.Core.Arguments.ArgumentSpecificationsFactory.Create(IList`1 argumentSpecs, Object[] arguments, IParameterInfo[] parameterInfos, MethodInfo methodInfo, MatchArgs matchArgs)
   at NSubstitute.Core.CallSpecificationFactory.CreateFrom(ICall call, MatchArgs matchArgs)
   at NSubstitute.Routing.Handlers.RecordCallSpecificationHandler.Handle(ICall call)
   at NSubstitute.Routing.Route.Handle(ICall call)
   at NSubstitute.Proxies.CastleDynamicProxy.CastleForwardingInterceptor.Intercept(IInvocation invocation)
   at Castle.DynamicProxy.AbstractInvocation.Proceed()
   at Castle.DynamicProxy.AbstractInvocation.Proceed()
   