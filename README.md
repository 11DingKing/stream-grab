# Stream Downloader - 直播流媒体下载器

## How to Run

### 快速开始（质检人员推荐）

```bash
# 1. 构建项目
docker compose build

# 2. 运行单元测试（73个测试用例）
docker compose --profile test run --rm test

# 3. 验证工具可用
docker run --rm stream-downloader --help

# 4. 一键测试（推荐）
chmod +x test-download.sh
./test-download.sh
```

### 手动测试下载

```bash
# 创建输出目录
mkdir -p output

# 下载 HLS 点播流（240p 快速测试，约 21MB）
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://test-streams.mux.dev/x36xhzz/url_2/193039199_mp4_h264_aac_ld_7.m3u8" \
    -o /app/output/test-video.ts -t 4

# 下载 HLS 点播流（1080p 完整测试，约 479MB，耗时较长）
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8" \
    -o /app/output/test-1080p.ts -t 4

# 查看下载结果
ls -la output/
```

## Services

| 服务 | 类型 | 说明 |
|------|------|------|
| stream-downloader | CLI工具 | 命令行流媒体下载器 |
| test | 测试服务 | 单元测试（73个测试用例） |

## 测试账号

本项目为命令行工具，无需账号登录。

## 题目内容

**用户需求：** 使用 C# 实现直播流媒体的下载

---

## 功能特性

- ✅ 支持 HLS (m3u8) 协议下载
- ✅ 支持 HTTP-FLV 直播流录制
- ✅ 支持 AES-128 加密流解密
- ✅ 多线程并发下载
- ✅ 断点续传
- ✅ 自动合并分片
- ✅ 自定义 HTTP Headers
- ✅ 代理服务器支持
- ✅ 实时进度显示（交互式终端使用进度条，非交互式使用文本输出）
- ✅ 详细日志记录
- ✅ 73个单元测试覆盖
- ✅ HLS直播流轮询机制 - 持续获取新分片
- ✅ FLV直播流录制 - 流式写入，无内存溢出
- ✅ 直播流自动识别 - 自动进入录制模式

## 命令行参数

```
StreamDownloader <url> [options]

Options:
  -o, --output <path>        输出文件路径 [default: output.ts]
  -t, --threads <num>        并发线程数 [default: 4]
  -d, --duration <seconds>   直播录制时长限制（0=无限制）
  --max-size <MB>            最大文件大小限制（0=无限制）
  --poll-interval <seconds>  直播流轮询间隔 [default: 5]
  --proxy <url>              代理服务器
  -H, --header <key:value>   自定义请求头
  -v, --verbose              详细日志
  --help                     显示帮助
```

## 使用示例

```bash
# 下载点播流（240p 快速测试）
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://test-streams.mux.dev/x36xhzz/url_2/193039199_mp4_h264_aac_ld_7.m3u8" \
    -o /app/output/video.ts

# 下载点播流（1080p，自动选择最高画质）
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8" \
    -o /app/output/video-hd.ts -t 4

# 录制直播流30秒
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://example.com/live.m3u8" -o /app/output/live.ts -d 30

# 录制直播流，最大100MB
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://example.com/live.flv" -o /app/output/live.flv --max-size 100

# 使用代理
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://example.com/live.m3u8" -o /app/output/video.ts \
    --proxy http://host.docker.internal:7890
```

---

## 质检验收指南

### 完整测试流程

#### 步骤 1: 构建镜像
```bash
docker compose build
```
**预期**: 构建成功

#### 步骤 2: 运行单元测试
```bash
docker compose --profile test run --rm test
```
**预期**: `Total tests: 73, Passed: 73`

#### 步骤 3: 验证 CLI
```bash
docker run --rm stream-downloader --help
docker run --rm stream-downloader --version
```
**预期**: 显示帮助信息，版本 `1.0.0`

#### 步骤 4: 测试点播下载
```bash
mkdir -p output
docker run --rm -it -v $(pwd)/output:/app/output stream-downloader \
    "https://test-streams.mux.dev/x36xhzz/url_2/193039199_mp4_h264_aac_ld_7.m3u8" \
    -o /app/output/test-vod.ts -t 4
ls -la output/test-vod.ts
```
**预期**: 下载成功，文件约 21MB（240p，64个分片）

### 验收清单

| # | 检查项 | 命令 | 预期结果 |
|---|--------|------|----------|
| 1 | Docker构建 | `docker compose build` | 成功 |
| 2 | 单元测试 | `docker compose --profile test run --rm test` | 73测试通过 |
| 3 | 帮助信息 | `docker run --rm stream-downloader --help` | 显示帮助 |
| 4 | 版本信息 | `docker run --rm stream-downloader --version` | 1.0.0 |
| 5 | 点播下载 | 步骤4 | 文件约21MB |

### 一键测试

```bash
chmod +x test-download.sh
./test-download.sh
```

---

## 技术栈

- .NET 8.0
- Serilog (日志)
- Spectre.Console (终端UI)
- System.CommandLine (命令行)
- xUnit + FluentAssertions (测试)

## License

MIT License
