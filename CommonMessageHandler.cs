using System;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hallam.Util.WebApi
{
    public class CommonMessageHandler : DelegatingHandler
    {
        private static readonly MediaTypeFormatter JsonFormatter = new JsonMediaTypeFormatter
        {
            SerializerSettings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return TransformResponse(request, await base.SendAsync(request, cancellationToken));
        }

        private static HttpResponseMessage TransformResponse(HttpRequestMessage request, HttpResponseMessage response)
        {
            object content;

            if (response.TryGetContentValue(out content))
            {
                if (content is string && !response.IsSuccessStatusCode)
                {
                    content = new { error = content };
                }
                else if (content is HttpError)
                {
                    var error = (HttpError)content;
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        error.Message = "Internal server error.";
                    }
                    content = TransformHttpError(error);
                }
                else
                {
                    content = new { result = content };
                }
            }
            else
            {
                content = new { };
            }

            if (content != null)
                response.Content = new ObjectContent(content.GetType(), content, JsonFormatter);

            return response;
        }

        private static object TransformHttpError(HttpError error)
        {
            dynamic result = new ExpandoObject();
            result.error = error.Message;
            result.errorDetail = error.MessageDetail;
            if (error.ModelState != null)
            {
                result.errors = error.ModelState.SelectMany(pair => (IEnumerable<string>) pair.Value).ToArray();
            }
#if DEBUG
            if (!string.IsNullOrEmpty(error.ExceptionMessage))
            {
                result.exception = ResolveExceptionDto(error);
            }
#endif
            return result;
        }

        private static object ResolveExceptionDto(HttpError error)
        {
            if (error == null) return null;
            dynamic r = new ExpandoObject();
            r.type = error.ExceptionType;
            r.message = error.ExceptionMessage;
            r.stackTrace = from line in error.StackTrace.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                           select line.Trim();
            r.innerException = ResolveExceptionDto(error.InnerException);
            return r;
        }
    }
}