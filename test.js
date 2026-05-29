const messages = [
  "Có phim hành động nào đang chiếu không?",
  "Avatar chiếu mấy giờ thứ 7?",
  "Có khuyến mãi gì không?",
  "Giá vé bao nhiêu?",
  "Chính sách hoàn vé thế nào?",
  "Xin chào",
  "thời tiết hôm nay"
];

async function run() {
  for (const msg of messages) {
    console.log(`\n--- Testing: ${msg} ---`);
    try {
      const res = await fetch("http://localhost:5231/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ history: [], message: msg })
      });
      const data = await res.json();
      console.log(`Bot Reply: ${data.reply}`);
    } catch (e) {
      console.log(`Error: ${e.message}`);
    }
    // wait 10s between requests to avoid Groq rate limit
    await new Promise(r => setTimeout(r, 10000));
  }
}

run();
