using System;

namespace EventSourceProxy.TestApp
{
    [EventSourceProxy.Generator.Proxy]
    public class MyClass
    {
        public virtual void Go()
        {
            Console.WriteLine("Yo!");
        }
    }
}
