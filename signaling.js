const fs = require("fs");
const http = require("http");
const hostname = process.env.HOST || "0.0.0.0";
const port = process.env.PORT || 3000;

// sdp dictionary
const rooms = {};

const server = http.createServer((request, response) => {
  // api routes
  const urlSdp = request.url.match(/^\/([a-zA-Z0-9]+)\/(offer|answer)\/?$/);
  if (urlSdp) {
    const roomId = urlSdp[1];
    const sdpType = urlSdp[2];
    const room = rooms[roomId] ?? { type: "", sdp: "" };

    // get (offer|answer) sdp
    if (request.method === "GET") {
      response.writeHead(200, { "Content-Type": "text/plain" });
      if (sdpType === room.type) { response.write(room.sdp); }
      response.end();
      return;
    }

    // set (offer|answer) sdp
    if (request.method === "POST") {
      response.writeHead(200);
      let text = "";
      request.on("data", data => { text += data; });
      request.on("end", () => {
        room.type = sdpType;
        room.sdp = decodeURIComponent(text.replace(/\+/g, '%20'));
        rooms[roomId] = room;
        console.log(`${room.type}: ${room.sdp}`);
      });
      response.end();
      return;
    }

    response.end();
    return;
  }

  // resource routes
  const path = (request.url === "/") ? "index.html" : request.url.slice(1);
  const resources = ["index.html", "index.js"]
  const mimeTypes = { "html": "text/html", "js": "text/javascript" };
  if (resources.includes(path)) {
    const html = fs.readFileSync(path);
    const mimeType = mimeTypes[path.split(".").slice(-1)[0].toLowerCase()] ?? "text/plain";
    response.writeHead(200, { "Content-Type": mimeType });
    response.end(html);
    return;
  }

  // other routes
  response.writeHead(404);
  response.end();
  return;
});

console.log(`listening on http://${hostname}:${port}/`);
server.listen(port, hostname);
