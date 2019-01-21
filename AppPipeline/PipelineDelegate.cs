using System.Threading.Tasks;

namespace AppPipeline {

    public delegate Task PipelineDelegate<in TContext>(TContext context);

}
