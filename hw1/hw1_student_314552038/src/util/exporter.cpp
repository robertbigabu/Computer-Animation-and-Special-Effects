#include "exporter.h"

#include <string>
#include <cstdio>
#include <sstream>

#include "filesystem.h"
#include "glad/glad.h"
#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb_image_write.h"
#include "iostream"

namespace util {
std::atomic<int> Writer::pictureCounter(-1);
std::atomic<int> Writer::filenameFormattingThreads(0);

std::string getTimestamp()
{
    auto now = std::chrono::system_clock::now();
    std::time_t t = std::chrono::system_clock::to_time_t(now);

    std::tm tm = *std::localtime(&t);

    std::ostringstream oss;
    oss << std::put_time(&tm, "%Y%m%d_%H%M%S");
    return oss.str();
}

namespace fs = std::filesystem;

void Exporter::exportVideo(int framerate)
{
    std::string timestamp = getTimestamp();

    fs::path videoDir = fs::current_path() / "Videos";
    if (!fs::exists(videoDir)) {
        fs::create_directory(videoDir);
    }

    fs::path videoPath = videoDir / (timestamp + ".mp4");

    std::string cmd =
            "ffmpeg -y -framerate " + std::to_string(framerate) + " -i ./Screenshots/%03d.jpg "
                                                                  "-c:v libx264 -pix_fmt yuv420p " + videoPath.string();

    std::cout << cmd << std::endl;

    std::system(cmd.c_str());
    std::cout << "Saved video to " << videoPath.string() << std::endl;
}

void Writer::infiniteLoop(int width, int height) {
    imageBuffer.resize(width * height * 3);
    char buf[32] = {};
    while (true) {
        {
            std::unique_lock<std::mutex> lock(notifyNew);
            cvNew.wait(lock, [this] { return hasData.load(); });
        }
        if (stop) break;
        ++filenameFormattingThreads;
        snprintf(buf, sizeof(buf), "./Screenshots/%03d.jpg", pictureCounter++);
        --filenameFormattingThreads;
        {
            std::unique_lock<std::mutex> lock(bufferLock);
            stbi_write_jpg(buf, width, height, 3, imageBuffer.data(), 80);
        }
        hasData.store(false);
        cvFinish.notify_one();
    }
}

void Writer::write() {
    if (pictureCounter.load() == -1) {
        ++pictureCounter;
        auto screenshotPath = fs::current_path() / "Screenshots";
        if (!fs::exists(screenshotPath)) {
            fs::create_directory(screenshotPath);
        } else {
            for (const auto& entry : fs::directory_iterator(screenshotPath)) {
                fs::remove_all(entry.path());
            }
        }
        return;
    }
    hasData.store(true);
    cvNew.notify_one();
}

void Writer::readCurrentPBO() {
    {
        std::unique_lock<std::mutex> lock(bufferLock);
        cvFinish.wait(lock, [this] { return !hasData.load(); });
    }
    glGetBufferSubData(GL_PIXEL_PACK_BUFFER, 0, imageBuffer.size(), imageBuffer.data());
}

Writer::Writer(int width, int height)
    : stop(false), hasData(false), writerThread(&Writer::infiniteLoop, this, width, height) {}

Writer::~Writer() { join(); }

void Writer::join() {
    if (writerThread.joinable()) {
        hasData.store(true);
        stop.store(true);
        cvNew.notify_one();
        writerThread.join();
    }
}

int Writer::getPictureCounter() { return pictureCounter.load(); }

void Writer::resetPictureCounter() {
    while (filenameFormattingThreads != 0) {
    }
    pictureCounter.store(-1);
}

Exporter::Exporter() {
    stbi_flip_vertically_on_write(true);
    glGenBuffers(2, pixelBuffer);
    writer.reserve(threadCount);
    printf("Using %d threads to write screenshot.\n", threadCount);
}

Exporter::~Exporter() { glDeleteBuffers(2, pixelBuffer); }

void Exporter::resize(int screenWidth, int screenHeight) {
    if (screenWidth & 0b11) {
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
    } else {
        glPixelStorei(GL_PACK_ALIGNMENT, 4);
    }
    writer.resize(0);
    width = screenWidth;
    height = screenHeight;
    glBindBuffer(GL_PIXEL_PACK_BUFFER, pixelBuffer[0]);
    glBufferData(GL_PIXEL_PACK_BUFFER, width * height * 3, nullptr, GL_STREAM_READ);
    glBindBuffer(GL_PIXEL_PACK_BUFFER, pixelBuffer[1]);
    glBufferData(GL_PIXEL_PACK_BUFFER, width * height * 3, nullptr, GL_STREAM_READ);
    glBindBuffer(GL_PIXEL_PACK_BUFFER, 0);
    for (int i = 0; i < threadCount; ++i) writer.emplace_back(std::make_unique<Writer>(width, height));
}

void Exporter::outputScreenShot() {
    glBindBuffer(GL_PIXEL_PACK_BUFFER, pixelBuffer[currentIndex]);
    glReadPixels(0, 0, width, height, GL_RGB, GL_UNSIGNED_BYTE, nullptr);
    currentIndex ^= 1;
    glBindBuffer(GL_PIXEL_PACK_BUFFER, pixelBuffer[currentIndex]);
    writer[currentWriter]->readCurrentPBO();
    glBindBuffer(GL_PIXEL_PACK_BUFFER, 0);
    writer[currentWriter]->write();
    (currentWriter += 1) %= threadCount;
}

}  // namespace util
