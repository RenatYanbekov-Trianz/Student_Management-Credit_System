@echo off
setlocal enabledelayedexpansion

:: ─────────────────────────────────────────────────────────────────────────────
:: deploy-image.bat — Deploy QLDSV_HTC_PROJECT to AWS EKS (Windows)
:: Usage: scripts\deploy-image.bat
:: Prerequisites: aws-cli, kubectl
:: ─────────────────────────────────────────────────────────────────────────────

set "APP_NAME=qldsv-htc-project"
set "NAMESPACE=qldsv-htc-project"
set "K8S_DIR=kubernetes"

echo ==============================================
echo   QLDSV_HTC_PROJECT -- Deploy to AWS EKS
echo ==============================================
echo.

:: ── Collect deployment inputs ─────────────────────────────────────────────────
set /p "AWS_REGION=Enter AWS Region (e.g. us-east-1): "
if "!AWS_REGION!"=="" (
    echo ERROR: AWS Region is required.
    exit /b 1
)

set /p "CLUSTER_NAME=Enter EKS Cluster Name: "
if "!CLUSTER_NAME!"=="" (
    echo ERROR: EKS Cluster Name is required.
    exit /b 1
)

set /p "IMAGE_URI=Enter full Docker image URI (e.g. 123456789.dkr.ecr.us-east-1.amazonaws.com/qldsv-htc-project:latest): "
if "!IMAGE_URI!"=="" (
    echo ERROR: Docker image URI is required.
    exit /b 1
)

echo.
echo --- Application Environment Variables ---
echo These will be stored in a Kubernetes Secret.
echo Press Enter to skip any variable.
echo.

set /p "DB_CONNECTION_STRING=Enter DB_CONNECTION_STRING (SQL Server connection string): "

:: ── Configure kubectl for EKS ─────────────────────────────────────────────────
echo.
echo Configuring kubectl for EKS cluster '!CLUSTER_NAME!' in '!AWS_REGION!'...
aws eks update-kubeconfig --region !AWS_REGION! --name !CLUSTER_NAME!
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to configure kubectl for EKS.
    exit /b 1
)

echo Verifying cluster connectivity...
kubectl cluster-info
if !ERRORLEVEL! neq 0 (
    echo ERROR: Cannot connect to EKS cluster.
    exit /b 1
)

:: ── Update manifests with actual image URI ────────────────────────────────────
echo.
echo Updating Kubernetes manifests...
powershell -NoProfile -Command "(Get-Content '!K8S_DIR!\deployment.yaml') -replace '{{IMAGE_URI}}', '!IMAGE_URI!' | Set-Content '!K8S_DIR!\deployment.yaml'"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to update deployment.yaml with image URI.
    exit /b 1
)

:: ── Apply Kubernetes manifests ────────────────────────────────────────────────
echo.
echo Applying namespace...
kubectl apply -f "!K8S_DIR!\namespace.yaml"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to apply namespace.
    exit /b 1
)

:: ── Create/update Kubernetes Secret ──────────────────────────────────────────
echo Creating/updating Kubernetes Secret...
kubectl create secret generic qldsv-htc-project-secrets --namespace="!NAMESPACE!" --from-literal=db-connection-string="!DB_CONNECTION_STRING!" --dry-run=client -o yaml | kubectl apply -f -
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to create/update Kubernetes Secret.
    exit /b 1
)

echo Applying deployment...
kubectl apply -f "!K8S_DIR!\deployment.yaml"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to apply deployment.
    exit /b 1
)

echo Applying service...
kubectl apply -f "!K8S_DIR!\service.yaml"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to apply service.
    exit /b 1
)

echo Applying ingress...
kubectl apply -f "!K8S_DIR!\ingress.yaml"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Failed to apply ingress.
    exit /b 1
)

:: ── Wait for rollout ──────────────────────────────────────────────────────────
echo.
echo Waiting for deployment rollout...
kubectl rollout status deployment/!APP_NAME! -n !NAMESPACE! --timeout=300s
if !ERRORLEVEL! neq 0 (
    echo ERROR: Deployment rollout failed or timed out.
    echo Run: kubectl rollout undo deployment/!APP_NAME! -n !NAMESPACE!
    exit /b 1
)

:: ── Verify resources ──────────────────────────────────────────────────────────
echo.
echo Verifying deployed resources...
kubectl get pods,svc,ingress -n !NAMESPACE!

echo.
echo ==============================================
echo   Deployment Complete!
echo   Check ingress for application URL:
echo     kubectl get ingress -n !NAMESPACE!
echo.
echo   Rollback command:
echo     kubectl rollout undo deployment/!APP_NAME! -n !NAMESPACE!
echo ==============================================

endlocal
