@echo off
setlocal enabledelayedexpansion

:: ─────────────────────────────────────────────────────────────────────────────
:: build-push.bat — Build and push QLDSV_HTC_PROJECT Docker image (Windows)
:: Usage: scripts\build-push.bat
:: Run from repository root (docker build context = .)
:: ─────────────────────────────────────────────────────────────────────────────

set "PROJECT_NAME=qldsv-htc-project"
set "DOCKERFILE_PATH=QLDSV_HTC_PROJECT\Dockerfile"

:: Sanitize image name via PowerShell
for /f "delims=" %%i in ('powershell -NoProfile -Command "$n = 'qldsv-htc-project' -replace '[^a-z0-9]','-'; $n = $n.ToLower().Trim('-'); Write-Output $n"') do set "IMAGE_NAME=%%i"

echo ==============================================
echo   QLDSV_HTC_PROJECT -- Docker Build ^& Push
echo ==============================================
echo.

:: ── Registry selection ────────────────────────────────────────────────────────
echo Select container registry:
echo   1. AWS ECR
echo   2. Docker Hub
echo.
set /p "REGISTRY_CHOICE=Enter choice [1-2]: "

:: ── Image tag ─────────────────────────────────────────────────────────────────
set /p "IMAGE_TAG_INPUT=Enter image tag (press Enter for 'latest'): "
if "!IMAGE_TAG_INPUT!"=="" (
    set "IMAGE_TAG=latest"
) else (
    for /f "delims=" %%t in ('powershell -NoProfile -Command "$t = '!IMAGE_TAG_INPUT!' -replace '[^a-z0-9._-]','-'; $t = $t.ToLower().Trim('-'); if ($t -eq '') { 'latest' } else { $t }"') do set "IMAGE_TAG=%%t"
)
echo Using tag: !IMAGE_TAG!
echo.

:: ── Registry-specific configuration ──────────────────────────────────────────
if "!REGISTRY_CHOICE!"=="1" (
    :: ── AWS ECR ──────────────────────────────────────────────────────────────
    echo --- AWS ECR Configuration ---
    set /p "AWS_REGION=Enter AWS Region (e.g. us-east-1): "
    set /p "AWS_ACCOUNT_ID=Enter AWS Account ID: "
    set /p "ECR_REPO_INPUT=Enter ECR repository name (default: !IMAGE_NAME!): "
    if "!ECR_REPO_INPUT!"=="" (
        set "ECR_REPO=!IMAGE_NAME!"
    ) else (
        set "ECR_REPO=!ECR_REPO_INPUT!"
    )

    set "REGISTRY_URL=!AWS_ACCOUNT_ID!.dkr.ecr.!AWS_REGION!.amazonaws.com"
    set "FULL_IMAGE_NAME=!REGISTRY_URL!/!ECR_REPO!:!IMAGE_TAG!"

    echo.
    echo Logging in to AWS ECR...
    aws ecr get-login-password --region !AWS_REGION! | docker login --username AWS --password-stdin !REGISTRY_URL!
    if !ERRORLEVEL! neq 0 (
        echo ERROR: ECR login failed.
        exit /b 1
    )

    :: Auto-create ECR repository if it does not exist
    echo Checking ECR repository '!ECR_REPO!'...
    aws ecr describe-repositories --repository-names !ECR_REPO! --region !AWS_REGION! >nul 2>&1
    if !ERRORLEVEL! neq 0 (
        echo Creating ECR repository '!ECR_REPO!'...
        aws ecr create-repository --repository-name !ECR_REPO! --region !AWS_REGION!
        if !ERRORLEVEL! neq 0 (
            echo ERROR: Failed to create ECR repository.
            exit /b 1
        )
    )
    echo ECR repository ready.

) else if "!REGISTRY_CHOICE!"=="2" (
    :: ── Docker Hub ────────────────────────────────────────────────────────────
    echo --- Docker Hub Configuration ---
    set /p "DOCKER_USERNAME=Enter Docker Hub username: "
    set /p "DOCKER_PASSWORD=Enter Docker Hub password/token: "
    set /p "DOCKER_REPO_INPUT=Enter Docker Hub repository (default: !DOCKER_USERNAME!/!IMAGE_NAME!): "
    if "!DOCKER_REPO_INPUT!"=="" (
        set "DOCKER_REPO=!DOCKER_USERNAME!/!IMAGE_NAME!"
    ) else (
        set "DOCKER_REPO=!DOCKER_REPO_INPUT!"
    )

    set "FULL_IMAGE_NAME=!DOCKER_REPO!:!IMAGE_TAG!"

    echo.
    echo Logging in to Docker Hub...
    echo !DOCKER_PASSWORD! | docker login --username !DOCKER_USERNAME! --password-stdin
    if !ERRORLEVEL! neq 0 (
        echo ERROR: Docker Hub login failed.
        exit /b 1
    )

) else (
    echo ERROR: Invalid registry choice. Exiting.
    exit /b 1
)

echo.
echo Building Docker image...
echo   Image  : !FULL_IMAGE_NAME!
echo   File   : !DOCKERFILE_PATH!
echo   Context: . (repository root)
echo.

docker build -f "!DOCKERFILE_PATH!" -t "!FULL_IMAGE_NAME!" .
if !ERRORLEVEL! neq 0 (
    echo ERROR: Docker build failed.
    exit /b 1
)

echo.
echo Pushing image to registry...
docker push "!FULL_IMAGE_NAME!"
if !ERRORLEVEL! neq 0 (
    echo ERROR: Docker push failed.
    exit /b 1
)

echo.
echo ==============================================
echo   Build ^& Push Complete!
echo   Image: !FULL_IMAGE_NAME!
echo ==============================================

endlocal
