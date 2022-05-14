using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using StickySessionHelperExample;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpForwarder();
builder.Services.AddHostedService<PodWatcher>();

var httpClient = new HttpMessageInvoker(new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
});
var transformer = HttpTransformer.Default;
var requestConfig = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };
Random rnd = new Random();

var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints => 
    endpoints.Map("/{**catch-all}", handler: async (HttpContext httpContext, IHttpForwarder forwarder) =>
    {
        var ipAddressNumber = rnd.Next(0, PodWatcher.IpAdresses.Count);
        var ipAddress = PodWatcher.IpAdresses[ipAddressNumber] as string;
        Console.WriteLine($"SEND CALL TO {ipAddress}");
        
        var error = await forwarder.SendAsync(httpContext, $"http://{ipAddress}/",
            httpClient, requestConfig, transformer);
        // Check if the operation was successful
        if (error != ForwarderError.None)
        {
            var errorFeature = httpContext.GetForwarderErrorFeature();
            var exception = errorFeature.Exception;
        }
    })
);

app.Run();