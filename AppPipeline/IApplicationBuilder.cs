using System;

namespace AppPipeline {
    public interface IApplicationBuilder<TContext> {
        PipelineDelegate<TContext> Build();
        IApplicationBuilder<TContext> Use(Func<PipelineDelegate<TContext>, PipelineDelegate<TContext>> middleware);

        IApplicationBuilder<TContext> UseMiddleware<TMiddleware>(params object[] args);
    }
}
