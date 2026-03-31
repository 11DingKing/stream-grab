#!/bin/bash

# Stream Downloader 测试脚本
# 用于质检人员快速验证下载功能

set -e

echo "=========================================="
echo "  Stream Downloader 功能测试"
echo "=========================================="
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 镜像名称
IMAGE_NAME="stream-downloader:latest"

# 测试流地址
# 使用 240p 子播放列表做快速验证（每个分片约 270KB，总计约 21MB）
# 1080p master playlist 总计约 479MB，下载耗时较长，仅在需要时手动测试
TEST_STREAM_240P="https://test-streams.mux.dev/x36xhzz/url_2/193039199_mp4_h264_aac_ld_7.m3u8"
TEST_STREAM_MASTER="https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"

# 创建输出目录
mkdir -p output logs

echo -e "${YELLOW}[1/5] 构建 Docker 镜像...${NC}"
docker compose build stream-downloader
echo -e "${GREEN}✓ 构建完成${NC}"
echo ""

echo -e "${YELLOW}[2/5] 运行单元测试...${NC}"
docker compose --profile test build test
if docker compose --profile test run --rm test; then
    echo -e "${GREEN}✓ 单元测试通过 (73个测试)${NC}"
else
    echo -e "${RED}✗ 单元测试失败${NC}"
    exit 1
fi
echo ""

echo -e "${YELLOW}[3/5] 验证帮助信息...${NC}"
docker run --rm ${IMAGE_NAME} --help
echo -e "${GREEN}✓ 帮助信息正常${NC}"
echo ""

echo -e "${YELLOW}[4/5] 验证版本信息...${NC}"
VERSION=$(docker run --rm ${IMAGE_NAME} --version)
echo "版本: $VERSION"
echo -e "${GREEN}✓ 版本信息正常${NC}"
echo ""

echo -e "${YELLOW}[5/5] 测试点播流下载（240p 快速验证）...${NC}"
echo "使用 240p 测试流: ${TEST_STREAM_240P}"
echo "（64个分片，约 21MB，预计 2-3 分钟）"
echo ""

# 清理之前的测试文件
rm -f output/test-video.ts

# 执行下载测试（脚本环境下不使用 -it，程序会自动切换为文本进度输出）
docker run --rm \
    -v "$(pwd)/output:/app/output" \
    -v "$(pwd)/logs:/app/logs" \
    ${IMAGE_NAME} \
    "${TEST_STREAM_240P}" \
    -o /app/output/test-video.ts \
    -t 4 \
    -v

# 检查下载结果
if [ -f "output/test-video.ts" ]; then
    FILE_SIZE=$(du -h output/test-video.ts | cut -f1)
    FILE_BYTES=$(wc -c < output/test-video.ts)
    echo ""
    echo -e "${GREEN}✓ 点播下载成功!${NC}"
    echo "  文件: output/test-video.ts"
    echo "  大小: $FILE_SIZE ($FILE_BYTES bytes)"
else
    echo -e "${RED}✗ 点播下载失败: 文件不存在${NC}"
    exit 1
fi
echo ""

echo "=========================================="
echo -e "${GREEN}  所有测试通过!${NC}"
echo "=========================================="
echo ""
echo "测试结果:"
echo "  ✓ Docker 构建成功"
echo "  ✓ 73个单元测试通过"
echo "  ✓ 命令行工具正常"
echo "  ✓ 点播流下载功能正常"
echo ""
echo "日志文件位于: logs/"
echo "下载文件位于: output/"
echo ""
echo "如需测试 1080p 完整下载（约 479MB），请手动执行:"
echo "  docker run --rm -it -v \$(pwd)/output:/app/output stream-downloader \\"
echo "      \"${TEST_STREAM_MASTER}\" -o /app/output/test-1080p.ts -t 4"

exit 0
