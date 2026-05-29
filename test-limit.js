async function run() {
    const res = await fetch('http://localhost:5231/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: 'có phim nào đang chiếu không', history: [] })
    });
    console.log("Status:", res.status);
    const text = await res.text();
    console.log("Body:", text);
}
run();
