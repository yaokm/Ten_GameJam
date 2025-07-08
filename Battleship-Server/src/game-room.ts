import { Room, Client } from "colyseus";

import { State, Player } from './game-state';
const paodanCount: number = 1;
export class GameRoom extends Room<State> {
    rematchCount: any = {};
    maxClients: number = 2;
    password: string;
    name: string;
    gridSize: number = 9;
    startingFleetHealth: number = 19;
    placements: any;
    playerHealth: any;
    playersPlaced: number = 0;
    playerCount: number = 0;

    onCreate(options) {
        console.log(options);
        if (options.password) {
            this.password = options.password;
            this.name = options.name;
            // this.setPrivate();
        }
        this.reset();
        this.setMetadata({ name: options.name || this.roomId, requiresPassword: !!this.password });
        this.onMessage("place", (client, message) => this.playerPlace(client, message));
        this.onMessage("turn", (client, message) => this.playerTurn(client, message));
        this.onMessage("rematch", (client, message) => this.rematch(client, message));
    }

    onJoin(client: Client) {
        let player: Player = new Player(client.sessionId, this.gridSize * this.gridSize, this.startingFleetHealth);
        this.state.players[client.sessionId] = player;
        this.playerCount++;

        if (this.playerCount == 2) {
            this.state.phase = 'place';
            this.lock();
        }
    }

    onAuth(client: Client, options: any) {
        if (!this.password) {
            return true
        };

        if (!options.password) {
            throw new Error("This room requires a password!")
        };

        if (this.password === options.password) {
            return true;
        }
        throw new Error("Invalid Password!");
    }

    onLeave(client: Client) {
        delete this.state.players[client.sessionId];
        delete this.playerHealth[client.sessionId];
        this.playerCount--;
        this.playersPlaced = 0;
        this.state.phase = 'waiting';
        this.placements = {};
        this.unlock();
    }

    playerPlace(client: Client, message: any) {
        let player: Player = this.state.players[client.sessionId];
        this.placements[player.sessionId] = message;
        this.playersPlaced++;

        if (this.playersPlaced == 2) {
            Object.keys(this.state.players).forEach(key => {
                this.playerHealth[this.state.players[key].sessionId] = this.startingFleetHealth;
            });
            this.state.playerTurn = this.getRandomUser();
            this.state.phase = 'battle';
        }
    }

    getRandomUser() {
        const keys = Object.keys(this.state.players);
        return this.state.players[keys[keys.length * Math.random() << 0]].sessionId;
    }

    getNextUser() {
        return this.state.players[Object.keys(this.state.players).filter(key => key != this.state.playerTurn)[0]];
    }

    playerTurn(client: Client, message: any) {
        const player: Player = this.state.players[client.sessionId];

        if (this.state.playerTurn != player.sessionId) return;

        const [targetIndex] = message;        // 只取 1 个坐标

        if (targetIndex === undefined) return; // Ensure targetIndex is defined

        if (targetIndex < 0 || targetIndex >= this.gridSize * this.gridSize) return; // Basic bounds check

        const enemy = this.getNextUser();

        let shots = player.shots;
        let targetShips = enemy.ships;;
        let targetedPlacement = this.placements[enemy.sessionId];

        let hit = false;                      // ← 记录是否命中

        if (shots[targetIndex] == -1) {
            shots[targetIndex] = this.state.currentTurn;
            if (targetedPlacement[targetIndex] >= 0) {
                hit = true;
                this.playerHealth[enemy.sessionId]--;
                switch (targetedPlacement[targetIndex]) {
                    case 0: // Admiral
                        this.updateShips(targetShips, 0, 5, this.state.currentTurn);
                        break;
                    case 1: // VTrio
                        this.updateShips(targetShips, 5, 8, this.state.currentTurn);
                        break;
                    case 2: // HTrio
                        this.updateShips(targetShips, 8, 11, this.state.currentTurn);
                        break;
                    case 3: // VDuo
                        this.updateShips(targetShips, 11, 13, this.state.currentTurn);
                        break;
                    case 4: // HDuo
                        this.updateShips(targetShips, 13, 15, this.state.currentTurn);
                        break;
                    case 5: // S
                    case 6: // S
                    case 7: // S
                    case 8: // S
                        this.updateShips(targetShips, 15, 19, this.state.currentTurn);
                        break;
                }
            }
        }

        if (this.playerHealth[enemy.sessionId] <= 0) {
            this.state.winningPlayer = player.sessionId;
            this.state.phase = 'result';
        } else {
            // 未命中：换对手并开始下一回合；命中：保持当前玩家，不递增回合
            if (!hit) {
                this.state.playerTurn = enemy.sessionId;
                this.state.currentTurn++;
            } else {
                // 触发状态刷新，确保客户端收到事件
                this.state.playerTurn = player.sessionId;
            }
        }
    }

    onDispose() { }

    rematch(client: Client, message: Boolean) {
        if (!message) {
            return this.state.phase = "leave";
        }

        this.state.players[client.sessionId].reset(this.gridSize * this.gridSize, this.startingFleetHealth);

        this.rematchCount[client.sessionId] = message;

        if (Object.keys(this.rematchCount).length == 2) {
            // this.reset(true);
            this.rematchCount = {};
            this.playerHealth = {};
            this.placements = {};
            this.state.playerTurn = "";
            this.state.winningPlayer = "";
            this.state.currentTurn = 1;
            this.playersPlaced = 0;
            this.state.phase = 'place';
        }
    }

    reset() {
        this.rematchCount = {};
        this.playerHealth = {};
        this.placements = {};
        this.playersPlaced = 0;
        let state = new State();
        state.phase = 'waiting';
        state.playerTurn = "";
        state.winningPlayer = "";
        state.currentTurn = 1;
        this.setState(state);
    }

    updateShips(arr: number[], s: number, e: number, t: number) {
        for (let i = s; i < e; i++) {
            if (arr[i] == -1) {
                arr[i] = t;
                break;
            }
        }
    }
}