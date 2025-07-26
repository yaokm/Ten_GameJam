import express from 'express';
import { createServer } from 'http';
import { Server } from 'colyseus';
import { monitor } from '@colyseus/monitor';
import { CustomLobbyRoom } from "./src/lobby-room";
import { GameRoom } from "./src/game-room";

const port = Number(process.env.PORT || 2567);
const app = express();

// Create WebSocket Server
const gameServer = new Server({
  server: createServer()
});

// Lobby
gameServer.define("lobby", CustomLobbyRoom);

// Define a room type
gameServer.define("game", GameRoom).filterBy(['password']).enableRealtimeListing();

// (optional) attach web monitoring panel
app.use('/cmon', monitor());

gameServer.onShutdown(function(){
  console.log(`game server is going down.`);
});

// 修改：绑定到 0.0.0.0 而不是 localhost，这样能接受来自任何 IP 的连接
gameServer.listen(port, "0.0.0.0");

console.log(`Listening on http://0.0.0.0:${ port }`);
