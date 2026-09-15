# QLDSV_HTC_PROJECT — Deployment Guide

## Overview

**Application**: QLDSV_HTC_PROJECT (Student Management System — Quản Lý Điểm Sinh Viên)  
**Technology**: .NET 8.0 Windows Forms (net8.0-windows)  
**Runtime Base Image**: `mcr.microsoft.com/dotnet/framework/runtime:4.7.2`  
**Build Image**: `mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022`  
**Target Platform**: AWS EKS (Elastic Kubernetes Service)  
**Health Endpoint**: `GET /health` on port `8080`

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Structure](#project-structure)
3. [Local Development with Docker Compose](#local-development-with-docker-compose)
4. [Build and Push Docker Image](#build-and-push-docker-image)
5. [AWS EKS Prerequisites](#aws-eks-prerequisites)
6. [EKS Cluster Setup](#eks-cluster-setup)
7. [Kubernetes Deployment Walkthrough](#kubernetes-deployment-walkthrough)
8. [Environment Variables and Secrets](#environment-variables-and-secrets)
9. [Health Checks and Monitoring](#health-checks-and-monitoring)
10. [Scaling and Management](#scaling-and-management)
11. [Troubleshooting](#troubleshooting)
12. [Security Considerations](#security-considerations)
13. [Rollback Procedures](#rollback-procedures)

---

## Prerequisites

### Local Development
- Docker Desktop (Windows containers enabled)
- .NET 8.0 SDK
- Windows 10/11 or Windows Server 2019/2022 (required for Windows containers)

### AWS EKS Deployment
- AWS CLI v2 (`aws --version`)
- kubectl (`kubectl version --client`)
- eksctl (optional, for cluster creation)
- IAM permissions: `eks:*`, `ecr:*`, `ec2:*`, `iam:PassRole`

---

## Project Structure

```
Management System/
├── QLDSV_HTC_PROJECT/
│   ├── Dockerfile                  # Multi-stage Windows container build
│   ├── .dockerignore               # Excludes bin/, obj/, .vs/, etc.
│   ├── QLDSV_HTC_PROJECT.csproj   # .NET 8.0 Windows Forms project
│   ├── Program.cs                  # Entry point + health check server startup
│   ├── Health/
│   │   └── HealthCheckServer.cs   # HTTP health check on port 8080 (/health)
│   ├── DAL/                        # Data Access Layer (SQL Server via SqlClient)
│   ├── BLL/                        # Business Logic Layer
│   ├── DTO/                        # Data Transfer Objects
│   └── GUI/                        # Windows Forms UI
├── docker-compose.yml              # Local development (app only)
├── kubernetes/
│   ├── namespace.yaml
│   ├── deployment.yaml
│   ├── service.yaml
│   └── ingress.yaml
├── scripts/
│   ├── build-push.sh               # Linux/macOS build & push
│   ├── build-push.bat              # Windows build & push
│   ├── deploy-image.sh             # Linux/macOS EKS deploy
│   └── deploy-image.bat            # Windows EKS deploy
└── docs/
    └── DEPLOYMENT.md               # This file
```

---

## Local Development with Docker Compose

> **Note**: Windows containers are required. Ensure Docker Desktop is switched to Windows containers mode.

### 1. Set environment variables

Create a `.env` file in the `Management System/` directory:

```env
DB_CONNECTION_STRING=Data Source=<SQL_SERVER_HOST>;Initial Catalog=QLDSV_HTC;User Id=<USER>;Password=<PASSWORD>;
```

### 2. Build and start the application

```bash
# From the "Management System/" directory
docker-compose up --build
```

### 3. Verify health check

```powershell
Invoke-WebRequest -Uri http://localhost:8080/health -UseBasicParsing
```

Expected response:
```json
{"status":"healthy","application":"QLDSV_HTC_PROJECT","timestamp":"2024-01-01T00:00:00.000Z"}
```

### 4. Stop the application

```bash
docker-compose down
```

---

## Build and Push Docker Image

### Linux/macOS

```bash
chmod +x scripts/build-push.sh
./scripts/build-push.sh
```

### Windows

```cmd
scripts\build-push.bat
```

The script will prompt you to:
1. Select registry type (AWS ECR or Docker Hub)
2. Enter registry credentials and details
3. Enter an image tag (defaults to `latest`)

The script automatically:
- Sanitizes the image name (lowercase, hyphens)
- Creates the ECR repository if it does not exist (ECR only)
- Builds from the repository root with `docker build -f QLDSV_HTC_PROJECT/Dockerfile -t <image> .`
- Pushes the image to the selected registry

---

## AWS EKS Prerequisites

### 1. Install AWS CLI

```bash
# Linux
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip && sudo ./aws/install

# macOS
brew install awscli

# Windows
# Download from https://aws.amazon.com/cli/
```

### 2. Configure AWS credentials

```bash
aws configure
# Enter: AWS Access Key ID, Secret Access Key, Region, Output format
```

### 3. Install kubectl

```bash
# Linux
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl && sudo mv kubectl /usr/local/bin/

# macOS
brew install kubectl

# Windows
choco install kubernetes-cli
```

### 4. Required IAM Permissions

Your IAM user/role needs:
- `eks:DescribeCluster`
- `eks:ListClusters`
- `ecr:GetAuthorizationToken`
- `ecr:BatchCheckLayerAvailability`
- `ecr:GetDownloadUrlForLayer`
- `ecr:BatchGetImage`
- `ecr:CreateRepository`
- `ecr:PutImage`

---

## EKS Cluster Setup

### Windows Node Group Requirement

QLDSV_HTC_PROJECT uses Windows containers. Your EKS cluster **must** have a Windows node group.

### 1. Create EKS cluster with Windows support (eksctl)

```bash
eksctl create cluster \
  --name my-eks-cluster \
  --region us-east-1 \
  --nodegroup-name linux-nodes \
  --node-type t3.medium \
  --nodes 2

# Add Windows node group
eksctl create nodegroup \
  --cluster my-eks-cluster \
  --region us-east-1 \
  --name windows-nodes \
  --node-type t3.xlarge \
  --nodes 2 \
  --node-ami-family WindowsServer2022FullContainer
```

### 2. Enable Windows support

```bash
kubectl apply -f https://amazon-eks.s3.us-west-2.amazonaws.com/manifests/us-west-2/vpc-resource-controller/latest/vpc-resource-controller-eks.yaml
```

### 3. Install AWS Load Balancer Controller (for Ingress)

```bash
# Add Helm repo
helm repo add eks https://aws.github.io/eks-charts
helm repo update

# Install controller
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=my-eks-cluster \
  --set serviceAccount.create=false \
  --set serviceAccount.name=aws-load-balancer-controller
```

### 4. Configure kubectl

```bash
aws eks update-kubeconfig --region us-east-1 --name my-eks-cluster
kubectl cluster-info
```

---

## Kubernetes Deployment Walkthrough

### Manifest Descriptions

| File | Description |
|------|-------------|
| `namespace.yaml` | Creates `qldsv-htc-project` namespace |
| `deployment.yaml` | 2-replica deployment with Windows node selector, health probes, resource limits |
| `service.yaml` | ClusterIP service exposing port 80 → container port 8080 |
| `ingress.yaml` | AWS ALB Ingress with health check path `/health` |

### Deploy using scripts

**Linux/macOS:**
```bash
chmod +x scripts/deploy-image.sh
./scripts/deploy-image.sh
```

**Windows:**
```cmd
scripts\deploy-image.bat
```

### Manual deployment

```bash
# 1. Apply namespace
kubectl apply -f kubernetes/namespace.yaml

# 2. Create secret with DB connection string
kubectl create secret generic qldsv-htc-project-secrets \
  --namespace=qldsv-htc-project \
  --from-literal=db-connection-string="Data Source=<HOST>;Initial Catalog=QLDSV_HTC;User Id=<USER>;Password=<PASS>;"

# 3. Update image URI in deployment.yaml
sed -i 's|{{IMAGE_URI}}|<YOUR_IMAGE_URI>|g' kubernetes/deployment.yaml

# 4. Apply manifests
kubectl apply -f kubernetes/deployment.yaml
kubectl apply -f kubernetes/service.yaml
kubectl apply -f kubernetes/ingress.yaml

# 5. Wait for rollout
kubectl rollout status deployment/qldsv-htc-project -n qldsv-htc-project

# 6. Verify
kubectl get pods,svc,ingress -n qldsv-htc-project
```

---

## Environment Variables and Secrets

| Variable | Source | Description |
|----------|--------|-------------|
| `DB_CONNECTION_STRING` | Kubernetes Secret `qldsv-htc-project-secrets` | Full SQL Server connection string |
| `HEALTH_CHECK_PORT` | Deployment env (default: `8080`) | Port for health check HTTP server |

### Using AWS Secrets Manager with IRSA

For production, use AWS Secrets Manager with IAM Roles for Service Accounts (IRSA):

```bash
# Store secret in AWS Secrets Manager
aws secretsmanager create-secret \
  --name qldsv-htc-project/db-connection-string \
  --secret-string "Data Source=<HOST>;Initial Catalog=QLDSV_HTC;User Id=<USER>;Password=<PASS>;"

# Install Secrets Store CSI Driver
helm repo add secrets-store-csi-driver https://kubernetes-sigs.github.io/secrets-store-csi-driver/charts
helm install csi-secrets-store secrets-store-csi-driver/secrets-store-csi-driver -n kube-system

# Install AWS Provider
kubectl apply -f https://raw.githubusercontent.com/aws/secrets-store-csi-driver-provider-aws/main/deployment/aws-provider-installer.yaml
```

---

## Health Checks and Monitoring

### Health Endpoint

The application exposes a health check endpoint at `GET /health` on port `8080`.

**Healthy response (HTTP 200):**
```json
{"status":"healthy","application":"QLDSV_HTC_PROJECT","timestamp":"2024-01-01T00:00:00.000Z"}
```

**Degraded response (HTTP 503)** — when SQL Server is unreachable:
```json
{"status":"degraded","application":"QLDSV_HTC_PROJECT","timestamp":"2024-01-01T00:00:00.000Z"}
```

### Kubernetes Probe Configuration

| Probe | Path | Port | Initial Delay | Period |
|-------|------|------|---------------|--------|
| Liveness | `/health` | 8080 | 60s | 30s |
| Readiness | `/health` | 8080 | 30s | 15s |

### Check pod health manually

```bash
kubectl exec -it <pod-name> -n qldsv-htc-project -- powershell -Command "Invoke-WebRequest -Uri http://localhost:8080/health -UseBasicParsing"
```

---

## Scaling and Management

### Manual scaling

```bash
kubectl scale deployment qldsv-htc-project --replicas=3 -n qldsv-htc-project
```

### Horizontal Pod Autoscaler (HPA)

```bash
kubectl autoscale deployment qldsv-htc-project \
  --namespace=qldsv-htc-project \
  --cpu-percent=70 \
  --min=2 \
  --max=10
```

### Rolling update (new image)

```bash
kubectl set image deployment/qldsv-htc-project \
  qldsv-htc-project=<NEW_IMAGE_URI> \
  -n qldsv-htc-project

kubectl rollout status deployment/qldsv-htc-project -n qldsv-htc-project
```

### View deployment history

```bash
kubectl rollout history deployment/qldsv-htc-project -n qldsv-htc-project
```

---

## Troubleshooting

### Pods not starting

```bash
# Check pod status
kubectl get pods -n qldsv-htc-project

# Describe pod for events
kubectl describe pod <pod-name> -n qldsv-htc-project

# View pod logs
kubectl logs <pod-name> -n qldsv-htc-project
kubectl logs <pod-name> -n qldsv-htc-project --previous
```

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `ImagePullBackOff` | ECR auth expired or wrong image URI | Re-run `aws ecr get-login-password` or check image URI |
| `CrashLoopBackOff` | App crash on startup | Check logs; verify `DB_CONNECTION_STRING` secret |
| `Pending` pods | No Windows nodes available | Verify Windows node group is running |
| Health probe failing | DB unreachable | Check SQL Server connectivity and `DB_CONNECTION_STRING` |
| `OOMKilled` | Memory limit exceeded | Increase `resources.limits.memory` in deployment.yaml |

### Windows container specific

```bash
# Verify Windows nodes are available
kubectl get nodes -l kubernetes.io/os=windows

# Check node selector in deployment
kubectl describe deployment qldsv-htc-project -n qldsv-htc-project | grep -A5 "Node-Selectors"
```

### Ingress not getting an address

```bash
# Check AWS Load Balancer Controller logs
kubectl logs -n kube-system -l app.kubernetes.io/name=aws-load-balancer-controller

# Verify ingress annotations
kubectl describe ingress qldsv-htc-project-ingress -n qldsv-htc-project
```

---

## Security Considerations

1. **Secrets Management**: Never store `DB_CONNECTION_STRING` in plain text. Use Kubernetes Secrets or AWS Secrets Manager with IRSA.
2. **Network Policies**: Restrict pod-to-pod communication using Kubernetes NetworkPolicy.
3. **Image Scanning**: Enable ECR image scanning to detect vulnerabilities.
4. **Least Privilege IAM**: Use IRSA with minimal IAM permissions for the pod's service account.
5. **TLS/HTTPS**: Configure HTTPS on the ALB Ingress using ACM certificates:
   ```yaml
   alb.ingress.kubernetes.io/certificate-arn: arn:aws:acm:us-east-1:123456789:certificate/xxx
   alb.ingress.kubernetes.io/listen-ports: '[{"HTTPS":443}]'
   ```
6. **Windows Container Security**: Windows containers run as `ContainerAdministrator` by default. Consider using `runAsNonRoot` where supported.

---

## Rollback Procedures

### Rollback to previous deployment

```bash
kubectl rollout undo deployment/qldsv-htc-project -n qldsv-htc-project
```

### Rollback to specific revision

```bash
kubectl rollout history deployment/qldsv-htc-project -n qldsv-htc-project
kubectl rollout undo deployment/qldsv-htc-project --to-revision=2 -n qldsv-htc-project
```

### Emergency: Delete and redeploy

```bash
kubectl delete deployment qldsv-htc-project -n qldsv-htc-project
kubectl apply -f kubernetes/deployment.yaml
```

---

## .NET-Specific Notes

- **Target Framework**: `net8.0-windows` — requires Windows container nodes in EKS
- **Runtime Base Image**: `mcr.microsoft.com/dotnet/framework/runtime:4.7.2` (explicit, as specified)
- **Build Image**: `mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022`
- **Health Check**: Custom `HealthCheckServer.cs` listens on `HEALTH_CHECK_PORT` (default 8080) at `/health`
- **Database**: SQL Server via `System.Data.SqlClient` 4.8.6 — connection string injected via `DB_CONNECTION_STRING` env var
- **Configuration**: `App.config` connection string is overridden by `DB_CONNECTION_STRING` environment variable at runtime
- **Build Artifacts**: `bin/` and `obj/` are excluded from Docker context via `.dockerignore`
- **Release Build**: PDB files and debug symbols are disabled in Release configuration to reduce image size
