# 多阶段构建 - 支持 ARM64 和 AMD64
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src

# 复制项目文件
COPY StreamDownloader/StreamDownloader.csproj StreamDownloader/
RUN dotnet restore StreamDownloader/StreamDownloader.csproj -a $TARGETARCH

# 复制源代码并构建
COPY . .
WORKDIR /src/StreamDownloader
RUN dotnet publish -c Release -o /app/publish -a $TARGETARCH --no-restore

# 运行时镜像
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# 创建非root用户
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

# 复制发布文件
COPY --from=build /app/publish .

# 创建输出目录和日志目录
RUN mkdir -p /app/output /app/logs && \
    chown -R appuser:appgroup /app

# 切换到非root用户
USER appuser

# 设置环境变量
ENV DOTNET_RUNNING_IN_CONTAINER=true

# 入口点
ENTRYPOINT ["dotnet", "StreamDownloader.dll"]
CMD ["--help"]
