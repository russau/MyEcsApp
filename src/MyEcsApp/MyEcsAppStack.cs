using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Constructs;

namespace MyEcsApp
{
    public class MyEcsAppStack : Stack
    {
        internal MyEcsAppStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {

           // Create a VPC
            var vpc = new Vpc(this, "MyVpc", new VpcProps
            {
                MaxAzs = 2, // Default is all AZs in the region
            });

            // Create an ECS Cluster with EC2 capacity
            var ecsCluster = new Cluster(this, "MyEcsCluster", new ClusterProps
            {
                Vpc = vpc,
            });

            // Add EC2 Capacity to the cluster
            ecsCluster.AddCapacity("MyAutoScalingGroup", new AddCapacityOptions
            {
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE2, InstanceSize.MICRO),
                DesiredCapacity = 2 // Desired number of EC2 instances
            });

            // Task Definition with container
            var taskDefinition = new Ec2TaskDefinition(this, "MyTaskDef");


            var container = taskDefinition.AddContainer("MyContainer", new ContainerDefinitionOptions
            {

                Image = ContainerImage.FromDockerImageAsset(new DockerImageAsset(this, "MyDockerImage", new DockerImageAssetProps
                {
                    Directory = "./app" // Path to your Dockerfile directory
                })),
                MemoryLimitMiB = 512,
                Cpu = 256,
                Logging = LogDrivers.AwsLogs(new AwsLogDriverProps
                {
                    StreamPrefix = "MyApp"
                })
            });

            container.AddPortMappings(new PortMapping
            {
                ContainerPort = 80,
                HostPort = 80,
                Protocol = Amazon.CDK.AWS.ECS.Protocol.TCP
            });

            // ECS Service using EC2
            var ecsService = new Ec2Service(this, "MyEcsService", new Ec2ServiceProps
            {
                Cluster = ecsCluster,
                TaskDefinition = taskDefinition,
            });

            // Create an Application Load Balancer (ALB)
            var alb = new ApplicationLoadBalancer(this, "MyALB", new ApplicationLoadBalancerProps
            {
                Vpc = vpc,
                InternetFacing = true,
            });

            // Create a listener for ALB
            var listener = alb.AddListener("MyListener", new BaseApplicationListenerProps
            {
                Port = 80,
                Open = true,
            });

            // Add the ECS service to the load balancer
            listener.AddTargets("EcsTargets", new AddApplicationTargetsProps
            {
                Port = 80,
                Targets = new[] { ecsService.LoadBalancerTarget(new LoadBalancerTargetOptions
                {
                    ContainerName = "MyContainer",
                    ContainerPort = 80
                }) },
                HealthCheck = new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
                {
                    Path = "/",
                    Interval = Duration.Seconds(60),
                    Timeout = Duration.Seconds(5),
                    HealthyThresholdCount = 2,
                    UnhealthyThresholdCount = 5
                }
            });

            // Output the ALB DNS Name
            new CfnOutput(this, "LoadBalancerDNS", new CfnOutputProps
            {
                Value = alb.LoadBalancerDnsName
            });
        }
    }
}
