using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace StickySessionHelperExample;

public class PodWatcher : BackgroundService
{
    // thread safe list
    public static ArrayList IpAdresses = ArrayList.Synchronized(new ArrayList());
    private readonly IConfiguration _configuration;

    public PodWatcher(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var inCluster = _configuration.GetValue("IN_CLUSTER", false);

        var config = inCluster
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile();

        IKubernetes client = new Kubernetes(config);

        var kubernetesNamespace = _configuration.GetValue("K8S_NAMESPACE_TO_WATCH", "default");

        var podlistResp = client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(kubernetesNamespace, watch: true, cancellationToken: stoppingToken);
        await foreach (var (type, item) in podlistResp.WatchAsync<V1Pod, V1PodList>().WithCancellation(stoppingToken))
        {
            Console.Write($"Event Pod {type} : {item.Metadata.Name} - {item.Status.Phase} - {item.Status.PodIP} [");
            foreach (var (key, value) in item.Metadata.Labels)
            {
                Console.Write($"{key}: {value}, ");    
            }
            
            Console.WriteLine("]");

            switch (type)
            {
                case WatchEventType.Added:
                    IpAdresses.Add(item.Status.PodIP);
                    break;
                case WatchEventType.Deleted:
                case WatchEventType.Error:
                    IpAdresses.Remove(item.Status.PodIP);
                    break;
            }
        }
    }
}