async function run() {
    const res = await fetch('http://localhost:5231/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: 'Chính sách hoàn vé thế nào?', history: [] })
    });
    const text = await res.text();
    console.log(text);
}
run();
