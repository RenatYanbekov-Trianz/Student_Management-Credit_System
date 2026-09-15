#!/bin/bash
set -e
set -o pipefail

# ─────────────────────────────────────────────────────────────────────────────
# deploy-image.sh — Deploy QLDSV_HTC_PROJECT to AWS EKS
# Usage: ./scripts/deploy-image.sh
# Prerequisites: aws-cli, kubectl
# ─────────────────────────────────────────────────────────────────────────────

APP_NAME="qldsv-htc-project"
NAMESPACE="qldsv-htc-project"
K8S_DIR="kubernetes"

echo "=============================================="
echo "  QLDSV_HTC_PROJECT — Deploy to AWS EKS"
echo "=============================================="
echo ""

# ── Collect deployment inputs ─────────────────────────────────────────────────
read -rp "Enter AWS Region (e.g. us-east-1): " AWS_REGION
if [ -z "$AWS_REGION" ]; then
  echo "ERROR: AWS Region is required."
  exit 1
fi

read -rp "Enter EKS Cluster Name: " CLUSTER_NAME
if [ -z "$CLUSTER_NAME" ]; then
  echo "ERROR: EKS Cluster Name is required."
  exit 1
fi

read -rp "Enter full Docker image URI (e.g. 123456789.dkr.ecr.us-east-1.amazonaws.com/qldsv-htc-project:latest): " IMAGE_URI
if [ -z "$IMAGE_URI" ]; then
  echo "ERROR: Docker image URI is required."
  exit 1
fi

echo ""
echo "--- Application Environment Variables ---"
echo "These will be stored in a Kubernetes Secret."
echo "Press Enter to skip any variable."
echo ""

read -rsp "Enter DB_CONNECTION_STRING (SQL Server connection string): " DB_CONNECTION_STRING
echo ""

# ── Configure kubectl for EKS ─────────────────────────────────────────────────
echo ""
echo "Configuring kubectl for EKS cluster '$CLUSTER_NAME' in '$AWS_REGION'..."
aws eks update-kubeconfig --region "$AWS_REGION" --name "$CLUSTER_NAME"

echo "Verifying cluster connectivity..."
kubectl cluster-info || { echo "ERROR: Cannot connect to EKS cluster."; exit 1; }

# ── Update manifests with actual image URI ────────────────────────────────────
echo ""
echo "Updating Kubernetes manifests..."
sed -i 's|{{IMAGE_URI}}|'"$IMAGE_URI"'|g' "$K8S_DIR/deployment.yaml"

# ── Apply Kubernetes manifests ────────────────────────────────────────────────
echo ""
echo "Applying namespace..."
kubectl apply -f "$K8S_DIR/namespace.yaml"

# ── Create/update Kubernetes Secret for sensitive env vars ────────────────────
echo "Creating/updating Kubernetes Secret..."
kubectl create secret generic qldsv-htc-project-secrets \
  --namespace="$NAMESPACE" \
  --from-literal=db-connection-string="${DB_CONNECTION_STRING}" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "Applying deployment..."
kubectl apply -f "$K8S_DIR/deployment.yaml"

echo "Applying service..."
kubectl apply -f "$K8S_DIR/service.yaml"

echo "Applying ingress..."
kubectl apply -f "$K8S_DIR/ingress.yaml"

# ── Wait for rollout ──────────────────────────────────────────────────────────
echo ""
echo "Waiting for deployment rollout..."
kubectl rollout status deployment/"$APP_NAME" -n "$NAMESPACE" --timeout=300s

# ── Verify resources ──────────────────────────────────────────────────────────
echo ""
echo "Verifying deployed resources..."
kubectl get pods,svc,ingress -n "$NAMESPACE"

# ── Display application URL ───────────────────────────────────────────────────
echo ""
INGRESS_HOST=$(kubectl get ingress "$APP_NAME-ingress" -n "$NAMESPACE" \
  -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || echo "pending")
echo "=============================================="
echo "  Deployment Complete!"
echo "  Application URL: http://$INGRESS_HOST"
echo "  Health Check   : http://$INGRESS_HOST/health"
echo ""
echo "  Rollback command:"
echo "    kubectl rollout undo deployment/$APP_NAME -n $NAMESPACE"
echo "=============================================="
