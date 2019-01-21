using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace AppPipeline {
    public class ApplicationBuilder<TContext> : IApplicationBuilder<TContext> {

        internal const string InvokeMethodName = "Invoke";
        internal const string InvokeAsyncMethodName = "InvokeAsync";

        private readonly IList<Func<PipelineDelegate<TContext>, PipelineDelegate<TContext>>> _components;

        public ApplicationBuilder() {
            _components = new List<Func<PipelineDelegate<TContext>, PipelineDelegate<TContext>>>();
        }

        public PipelineDelegate<TContext> Build() {

            PipelineDelegate<TContext> app = context => Task.CompletedTask;

            foreach (var component in _components.Reverse()) {
                app = component(app);
            }

            return app;
        }

        public IApplicationBuilder<TContext> Use(Func<PipelineDelegate<TContext>, PipelineDelegate<TContext>> middleware) {
            _components.Add(middleware);
            return this;
        }

        public IApplicationBuilder<TContext> UseMiddleware<TMiddleware>(params object[] args) {
            var middleware = typeof(TMiddleware);
            return Use(next => {

                var methods = middleware.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                var invokeMethods = methods.Where(m =>
                    string.Equals(m.Name, InvokeMethodName, StringComparison.Ordinal)
                    || string.Equals(m.Name, InvokeAsyncMethodName, StringComparison.Ordinal)
                ).ToArray();


                if (invokeMethods.Length > 1) {
                    throw new InvalidOperationException("Use middle mutliple invokes.");
                }

                if (invokeMethods.Length == 0) {
                    throw new InvalidOperationException("Use middleware no invoke method");
                }

                var methodInfo = invokeMethods[0];
                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType)) {
                    throw new InvalidOperationException("Use middleware non task return type");
                }


                var parameters = methodInfo.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(TContext)) {
                    throw new InvalidOperationException("Use middleware no parameters");
                }


                var ctorArgs = new object[args.Length + 1];
                ctorArgs[0] = next;
                Array.Copy(args, 0, ctorArgs, 1, args.Length);

                var instance = Activator.CreateInstance(middleware, ctorArgs);
                if (parameters.Length == 1) {
                    return (PipelineDelegate<TContext>)methodInfo.CreateDelegate(typeof(PipelineDelegate<TContext>), instance);
                }

                var factory = Compile<object>(methodInfo, parameters);


                return context => factory(instance, context);
            });
        }

        private static Func<T, TContext, Task> Compile<T>(MethodInfo methodInfo,
            ParameterInfo[] parameters) {

            var middleware = typeof(T);
            var contextArg = Expression.Parameter(typeof(TContext), "context");
            var instanceArg = Expression.Parameter(middleware, "middleware");
            var methodArguments = new Expression[parameters.Length];
            methodArguments[0] = contextArg;

            for (int i = 1; i < parameters.Length; i++) {

                var parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef) {
                    throw new NotSupportedException("Invoke does not support ref or out params");
                }
            }

            Expression middlewareInstanceArg = instanceArg;
            if (methodInfo.DeclaringType != typeof(T)) {
                middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
            }

            var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);

            var lambda = Expression.Lambda<Func<T, TContext, Task>>(body, instanceArg, contextArg);

            return lambda.Compile();
        }
    }
}
