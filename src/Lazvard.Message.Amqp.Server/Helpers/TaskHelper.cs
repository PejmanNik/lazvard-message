namespace Lazvard.Message.Amqp.Server.Helpers
{
    static internal class TaskHelper
    {
        public static async ValueTask WhenAll(params ValueTask[] tasks)
        {
            if (tasks.Length == 0)
                return;

            for (var i = 0; i < tasks.Length; i++)
            {
                await tasks[i].ConfigureAwait(false);
            }
        }
    }
}
