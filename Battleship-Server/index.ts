import express from 'express';
import { createServer } from 'http';
import { Server } from 'colyseus';
import { monitor } from '@colyseus/monitor';
import { CustomLobbyRoom } from "./src/lobby-room";
import { GameRoom } from "./src/game-room";

const port = Number(process.env.PORT || 2567);
const app = express();
app.use(express.json());

const httpServer = createServer(app);
//httpServer.on('request', app);

const gameServer = new Server({
  server: httpServer
});

gameServer.define("lobby", CustomLobbyRoom);
gameServer.define("game", GameRoom).filterBy(['password']).enableRealtimeListing();

// (optional) attach web monitoring panel
// app.use('/cmon', monitor());

gameServer.onShutdown(function(){
  console.log(`game server is going down.`);
});

  gameServer.listen(port, '0.0.0.0');

  console.log(`Listening on http://0.0.0.0:${ port }`);
  console.log(`Server should accept connections from any IP address`);
  console.log(`Test with: ws://localhost:${port} or ws://192.168.87.54:${port}`);
;

app.get('/test', (req, res) => res.send('ok'));
