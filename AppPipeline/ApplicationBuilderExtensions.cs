using System;
using System.Threading.Tasks;

namespace AppPipeline {
    public static class ApplicationBuilderExtensions {

        internal const string InvokeMethodName = "Invoke";
        internal const string InvokeAsyncMethodName = "InvokeAsync";

        public static void Run<TContext>(this IApplicationBuilder<TContext> app, PipelineDelegate<TContext> handler) {

            if (app == null) {
                throw new ArgumentNullException(nameof(app));
            }

            if (handler == null) {
                throw new ArgumentNullException(nameof(handler));
            }

            app.Use(_ => handler);
        }

        public static IApplicationBuilder<TContext> Use<TContext>(this IApplicationBuilder<TContext> app, Func<TContext, Func<Task>, Task> middleware) {
            return app.Use(next => {
                return context => {
                    Func<Task> simpleNext = () => next(context);
                    return middleware(context, simpleNext);
                };
            });
        }
    }
}
