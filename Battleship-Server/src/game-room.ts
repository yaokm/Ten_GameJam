import { Room, Client } from "colyseus";

import { State, Player } from './game-state';
import axios from "axios";
import * as fs from "fs";
const config = JSON.parse(fs.readFileSync(require("path").resolve(__dirname, "./config.json"), "utf-8"));
const paodanCount: number = 1;
export class GameRoom extends Room<State> {
    rematchCount: any = {};
    maxClients: number = 2;
    password: string;
    name: string;
    gridSize: number = 10;
    startingFleetHealth: number = 24;
    placements: any={};
    playerHealth: any={};
    playersPlaced: number = 0;
    playerCount: number = 0;
    eDirections: any={};//敌军方向
    eBasePositions: any={};//敌军基座位置
    playerSkipNextTurn: {[key: string]: boolean} = {}; // 用于跟踪玩家是否需要跳过下一回合
    multiShotDirections: { [key: string]: string } = {};
    isAIMode: boolean = false;
    aiSessionId: string = "AI";
    aiPlaced: boolean = false;
    aiPromptHistory: any[] = [];
    aiRound: number = 1;
    onCreate(options) {
        console.log(options);
        if (options.password) {
            this.password = options.password;
            this.name = options.name;
            // this.setPrivate();
        }
        this.isAIMode = !!options.aiMode;
        this.reset();
        this.setMetadata({ name: options.name || this.roomId, requiresPassword: !!this.password });
        this.onMessage("place", (client, message) => this.playerPlace(client, message));
        this.onMessage("turn", (client, message) => this.playerTurn(client, message));
        this.onMessage("rematch", (client, message) => this.rematch(client, message));
        this.onMessage("direction", (client, message) => this.playerDirection(client, message));
        // 添加对opponentInfoRequest消息的处理
        this.onMessage("opponentInfoRequest", (client, message) => this.handleOpponentInfoRequest(client));
        // 注册useSkill消息
        this.onMessage("useSkill", (client, message) => this.handleUseSkill(client, message));
    }
    // 添加新方法处理对手信息请求
    handleOpponentInfoRequest(client: Client) {
        // 获取当前玩家
        const player = this.state.players[client.sessionId];

        // 获取对手
        const enemyId = Object.keys(this.state.players).find(id => id !== client.sessionId);
        if (!enemyId) return; // 如果没有对手，直接返回

        // 获取对手的方向和基点信息
        const enemyDirections = this.eDirections[enemyId] || [];
        const enemyBasePositions = this.eBasePositions[enemyId] || [];

        // 发送对手信息给请求的客户端
        client.send("opponentInfo", {
            directions: enemyDirections,
            basePositions: enemyBasePositions
        });
    }
    onJoin(client: Client) {
        let player: Player = new Player(client.sessionId, this.gridSize * this.gridSize, this.startingFleetHealth);
        this.state.players[client.sessionId] = player;
        this.playerCount++;

        if (this.isAIMode && this.playerCount === 1) {
            // 自动加入AI
            let aiPlayer = new Player(this.aiSessionId, this.gridSize * this.gridSize, this.startingFleetHealth);
            this.state.players[this.aiSessionId] = aiPlayer;
            this.playerCount++;
            // AI自动布阵
            this.aiPlaceFleet();
            this.aiPlaced = true;
            this.state.phase = 'place';
            this.lock();
        }

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
        //console.log("Received message:", JSON.stringify(message));
            // 确保对象已初始化
        this.placements = this.placements || {};
        this.eDirections = this.eDirections || {};
        this.eBasePositions = this.eBasePositions || {};
        //cell,direction,basePositions
        this.placements[player.sessionId] = message.placement;
        this.eDirections[player.sessionId] = message.directions;
        this.eBasePositions[player.sessionId] = message.basePositions;
        
        this.playersPlaced++;

        if (this.isAIMode && !this.aiPlaced && this.playersPlaced === 1) {
            this.aiPlaceFleet();
            this.aiPlaced = true;
        }

        if (this.playersPlaced == 2) {
            Object.keys(this.state.players).forEach(key => {
                this.playerHealth[this.state.players[key].sessionId] = this.startingFleetHealth;
            });
            this.state.playerTurn = this.getRandomUser();
            this.state.phase = 'battle';
            // 双方都布阵完成后，主动向双方发送对方的船只信息
            Object.keys(this.state.players).forEach(playerId => {
                const enemyId = Object.keys(this.state.players).find(id => id !== playerId);
                console.log("enemyId:", enemyId);
                if (enemyId) {
                    const client = this.clients.find(c => c.sessionId === playerId);
                    if (client) {
                        console.log("eDirections:", this.eDirections[enemyId]);
                        console.log("eBasePositions:", this.eBasePositions[enemyId]);
                        client.send("opponentInfo", {
                            directions: this.eDirections[enemyId] || [],
                            basePositions: this.eBasePositions[enemyId] || []
                        });
                    }
                }
            });
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
        let targetShips = enemy.ships;
        let targetedPlacement = this.placements[enemy.sessionId];

        let hit = false;                      // ← 记录是否命中
        let isXBomb = false;                  // ← 新增：记录是否命中X炸弹船

        // 多方向开火处理
        let multiShotDir = this.multiShotDirections && this.multiShotDirections[player.sessionId];
        if (multiShotDir && message.length === 2) {
            // message为两个格子索引
            for (let i = 0; i < 2; i++) {
                const idx = message[i];
                if (shots[idx] == -1) {
                    shots[idx] = this.state.currentTurn;
                    if (targetedPlacement[idx] >= 0) {
                        hit = true;
                        this.playerHealth[enemy.sessionId]--;
                        // 这里只处理一次updateShips，实际可根据需求调整
                    }
                }
            }
            // 用完后清除
            delete this.multiShotDirections[player.sessionId];
        } else {
            if (shots[targetIndex] == -1) {
                shots[targetIndex] = this.state.currentTurn;
                if (targetedPlacement[targetIndex] >= 0) {
                    hit = true;
                    this.playerHealth[enemy.sessionId]--;
                    switch (targetedPlacement[targetIndex]) {
                        case 0: // F0
                            this.updateShips(targetShips, 0, 6, this.state.currentTurn);
                            break;
                        case 1: // E0
                            this.updateShips(targetShips, 6, 11, this.state.currentTurn);
                            break;
                        case 2: // D0
                            this.updateShips(targetShips, 11, 15, this.state.currentTurn);
                            break;
                        case 3: // C0
                            this.updateShips(targetShips, 15, 18, this.state.currentTurn);
                            break;
                        case 4: // B0
                            this.updateShips(targetShips, 18, 20, this.state.currentTurn);
                            break;
                        // case 5: // A0
                        //     this.updateShips(targetShips, 20, 21, this.state.currentTurn);
                        //     break;
                        case 5: // D1
                            this.updateShips(targetShips, 20, 24, this.state.currentTurn);
                            break;
                        case 6: // X，是个炸弹，踩到会卡对面一回合
                            isXBomb = true;  // 标记命中了X炸弹
                            this.playerSkipNextTurn[player.sessionId] = true; // 修改为标记当前玩家需要跳过下一回合
                            hit = false; // 踩到炸弹不算命中，需要结束回合
                            console.log("踩中炸弹了")
                            // 发送一个特殊消息通知客户端，玩家被炸弹影响
                            this.broadcast("xBombHit", {
                                player: player.sessionId,
                                victim: player.sessionId // 受害者是玩家自己
                            });
                            break;
                        case 8: // S
                            break;
                    }
                }
            }
        }

        if (this.playerHealth[enemy.sessionId] <= 0) {
            this.state.winningPlayer = player.sessionId;
            this.state.phase = 'result';
        } else {
            // 未命中或踩到炸弹：换对手并开始下一回合；命中普通船：保持当前玩家，不递增回合
            if (!hit) {
                // 正常切换回合
                this.state.playerTurn = enemy.sessionId;
                this.state.currentTurn++;
                
                // 检查下一回合的玩家是否需要跳过回合
                if (this.playerSkipNextTurn[enemy.sessionId]) {
                    // 对手需要跳过下一回合，直接切换回玩家
                    this.playerSkipNextTurn[enemy.sessionId] = false; // 重置跳过标记
                    this.state.playerTurn = player.sessionId; // 回合切回当前玩家
                    this.state.currentTurn++; // 回合数再次增加
                    // 发送通知消息
                    this.broadcast("skipTurn", {
                        player: enemy.sessionId
                    });
                }
            } else {
                // 命中普通船：保持当前玩家回合
                this.state.playerTurn = player.sessionId;
            }
        }

        // AI对战：如果轮到AI，自动触发AI回合
        if (this.isAIMode && this.state.playerTurn === this.aiSessionId && this.state.phase === 'battle') {
            this.triggerAITurn();
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
    playerDirection(client: Client, message: any) {
        const player: Player = this.state.players[client.sessionId];
        this.eDirections[player.sessionId] = message;
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
        this.eDirections = {};
        this.eBasePositions = {};
    }

    updateShips(arr: number[], s: number, e: number, t: number) {
        for (let i = s; i < e; i++) {
            if (arr[i] == -1) {
                arr[i] = t;
                break;
            }
        }
    }

    // 处理技能使用
    handleUseSkill(client: Client, message: any) {
        const player: Player = this.state.players[client.sessionId];
        const skillType = message.skillType;
        if (typeof skillType !== 'number' || skillType < 1 || skillType > 4) return;
        // 只允许自己回合用技能
        if (this.state.playerTurn !== player.sessionId) return;
        // 每种技能每局只能用一次
        if (player.usedSkills[skillType - 1] === 1) return;
        player.usedSkills[skillType - 1] = 1;
        const enemyId = Object.keys(this.state.players).find(id => id !== client.sessionId);
        const enemy = this.state.players[enemyId];
        let params = null;
        if (skillType === 1) {
            // ①令对面一个回合不能行动
            this.playerSkipNextTurn[enemyId] = true;
            params = { effect: 'stun', target: enemyId };
        } else if (skillType === 2) {
            // ②随机照明2*3未被点击区域，显示有几种船的部位存在
            // 获取所有未被点击的格子
            const shots = player.shots;
            const areaSize = this.gridSize;
            let found = false;
            let tryCount = 0;
            let region = null;
            let shipTypes = new Set();
            const placement = this.placements[enemyId];
            while (!found && tryCount < 100) {
                // 随机左上角
                const x = Math.floor(Math.random() * (areaSize - 2));
                const y = Math.floor(Math.random() * (areaSize - 3));
                let allUnshot = true;
                let localTypes = new Set();
                for (let dx = 0; dx < 2; dx++) {
                    for (let dy = 0; dy < 3; dy++) {
                        const idx = (y + dy) * areaSize + (x + dx);
                        if (shots[idx] !== -1) {
                            allUnshot = false;
                            break;
                        }
                        if (placement[idx] >= 0) {
                            localTypes.add(placement[idx]);
                        }
                    }
                    if (!allUnshot) break;
                }
                if (allUnshot) {
                    found = true;
                    region = { x, y };
                    shipTypes = localTypes;
                }
                tryCount++;
            }
            params = { effect: 'scan', region, shipTypeCount: shipTypes.size };
        } else if (skillType === 3) {
            // ③随机爆出对面一个未被找过的船的点位
            const shots = player.shots;
            const placement = this.placements[enemyId];
            const unshotShipCells = [];
            for (let i = 0; i < placement.length; i++) {
                if (placement[i] >= 0 && shots[i] === -1) {
                    unshotShipCells.push(i);
                }
            }
            let revealIdx = null;
            if (unshotShipCells.length > 0) {
                revealIdx = unshotShipCells[Math.floor(Math.random() * unshotShipCells.length)];
            }
            params = { effect: 'reveal', cellIndex: revealIdx };
        } else if (skillType === 4) {
            // ④开火时可选择上下左右任意一个位置一起开火
            // 只记录本回合生效，实际射击时在playerTurn处理
            params = { effect: 'multishot', direction: message.params && message.params.direction };
            // 记录到GameRoom的multiShotDirections，供playerTurn读取
            this.multiShotDirections[player.sessionId] = message.params && message.params.direction;
        }
        // 广播技能使用和效果
        console.log("skillUsed params:", JSON.stringify(params));
        this.broadcast("skillUsed", {
            player: player.sessionId,
            skillType: skillType,
            params: params
        });
    }

    aiPlaceFleet() {
        // 使用更智能的随机布阵，参考Python代码思路
        const size = this.gridSize * this.gridSize;
        const placement = new Array(size).fill(-1);
        const occupied = new Array(size).fill(0); // 占用矩阵
        
        // 船只配置 [ship_id, shape, ship_index]
        const ships = [
            { id: "A0", shape: [[1,0]], shipIndex: 0 },                    // 1格
            { id: "B0", shape: [[1,0],[1,0]], shipIndex: 1 },              // 2格直线
            { id: "C0", shape: [[1,0],[1,0],[1,0]], shipIndex: 2 },     // 3格直线
            { id: "D0", shape: [[1,0],[1,1],[1,0]], shipIndex: 3 },     // T型4格
            { id: "D1", shape: [[1,0],[1,0],[1,0],[1,0]], shipIndex: 4 }, // 4格直线
            { id: "E0", shape: [[1,0],[1,0],[1,0],[1,0],[1,0]], shipIndex: 5 }, // 5格直线
            { id: "F0", shape: [[1,0],[1,1],[1,1],[0,1]], shipIndex: 6 }, // Z型6格
        ];

        // 将shape格式转换为坐标位置列表
        const shapeToPositions = (shape: number[][]): [number, number][] => {
            const positions: [number, number][] = [];
            for (let row = 0; row < shape.length; row++) {
                const [left, right] = shape[row];
                if (left === 1) {
                    positions.push([row, 0]); // 左边有舰船
                }
                if (right === 1) {
                    positions.push([row, 1]); // 右边有舰船
                }
            }
            return positions;
        };

        // 旋转位置坐标 - 90度顺时针旋转
        const rotatePositions = (positions: [number, number][], times: number): [number, number][] => {
            let result = [...positions];
            for (let i = 0; i < times % 4; i++) {
                // 90度顺时针旋转: (row,col) -> (col, -row)
                result = result.map(([row, col]) => [col, -row]);
            }
            return result;
        };

        // 标准化位置，使最小坐标为(0,0)
        const normalizePositions = (positions: [number, number][]): [number, number][] => {
            if (positions.length === 0) return positions;
            const minRow = Math.min(...positions.map(pos => pos[0]));
            const minCol = Math.min(...positions.map(pos => pos[1]));
            return positions.map(([row, col]) => [row - minRow, col - minCol]);
        };

        // 检查是否可以放置舰船
        const canPlaceShip = (occupied: number[], positions: [number, number][], startRow: number, startCol: number): boolean => {
            const tempOccupied = [...occupied];
            for (const [rowOffset, colOffset] of positions) {
                const row = startRow + rowOffset;
                const col = startCol + colOffset;
                // 检查边界
                if (row < 0 || row >= this.gridSize || col < 0 || col >= this.gridSize) {
                    return false;
                }
                // 检查是否会产生重叠
                const idx = row * this.gridSize + col;
                tempOccupied[idx]++;
                if (tempOccupied[idx] > 1) {
                    return false;
                }
            }
            return true;
        };

        // 放置舰船
        const placeShip = (placement: number[], occupied: number[], shipIndex: number, positions: [number, number][], startRow: number, startCol: number) => {
            for (const [rowOffset, colOffset] of positions) {
                const row = startRow + rowOffset;
                const col = startCol + colOffset;
                const idx = row * this.gridSize + col;
                placement[idx] = shipIndex;
                occupied[idx] = 1;
            }
        };

        // 获取位置边界
        const getBounds = (positions: [number, number][]): [number, number] => {
            if (positions.length === 0) return [0, 0];
            const maxRow = Math.max(...positions.map(pos => pos[0]));
            const maxCol = Math.max(...positions.map(pos => pos[1]));
            return [maxRow, maxCol];
        };

        // 随机放置每个舰船
        for (const ship of ships) {
            let placed = false;
            let attempts = 0;
            
            while (!placed && attempts < 1000) {
                // 将shape转换为位置
                const basePositions = shapeToPositions(ship.shape);
                // 随机旋转
                const rotation = Math.floor(Math.random() * 4);
                const rotatedPositions = rotatePositions(basePositions, rotation);
                const normalizedPositions = normalizePositions(rotatedPositions);
                // 获取边界
                const [maxRow, maxCol] = getBounds(normalizedPositions);
                
                // 随机选择起始位置
                if (maxRow < this.gridSize && maxCol < this.gridSize) {
                    const startRow = Math.floor(Math.random() * (this.gridSize - maxRow));
                    const startCol = Math.floor(Math.random() * (this.gridSize - maxCol));
                    
                    // 检查是否可以放置（不会产生重叠）
                    if (canPlaceShip(occupied, normalizedPositions, startRow, startCol)) {
                        placeShip(placement, occupied, ship.shipIndex, normalizedPositions, startRow, startCol);
                        placed = true;
                    }
                }
                attempts++;
            }
            
            if (!placed) {
                console.warn(`警告: 无法放置 ${ship.id}`);
            }
        }

        this.placements[this.aiSessionId] = placement;
        this.eDirections[this.aiSessionId] = [0,0,0,0,0,0,0]; // 默认方向
        this.eBasePositions[this.aiSessionId] = [0,0,0,0,0,0,0]; // 默认基点
        this.playersPlaced++;
    }

    async triggerAITurn() {
        // 1. 组织棋盘状态
        const playerId = Object.keys(this.state.players).find(id => id !== this.aiSessionId);
        const player = this.state.players[playerId];
        const ai = this.state.players[this.aiSessionId];
        // 构造10x10棋盘，-1未探索，0空地，1击中，A0~F0为击沉
        const shots = ai.shots;
        const placement = this.placements[playerId];
        let board = [];
        for (let i = 0; i < this.gridSize; i++) {
            let row = [];
            for (let j = 0; j < this.gridSize; j++) {
                let idx = i * this.gridSize + j;
                if (shots[idx] === -1) {
                    row.push("-1");
                } else if (placement[idx] === -1) {
                    row.push("0");
                } else if (shots[idx] !== -1 && placement[idx] >= 0) {
                    // 判断是否击沉
                    // 这里只做简单处理，实际可更细致
                    row.push("1");
                } else {
                    row.push("0");
                }
            }
            board.push(row);
        }
        // 格式化棋盘
        let board_state = "   " + Array.from({length:10}, (_,i)=>String.fromCharCode(65+i)).join(" ") + "\n";
        for (let i = 0; i < 10; i++) {
            board_state += `${i+1}`.padStart(2, ' ') + ": " + board[i].map(cell=>cell.padStart(2,' ')).join(" ") + "\n";
        }
        // 2. 组织prompt
        const round_number = this.aiRound;
        const user_prompt = config.prompts.user_prompt_template.replace("{round_number}", round_number).replace("{board_state}", board_state);
        const messages = [
            { role: "system", content: config.prompts.system_prompt },
            ...this.aiPromptHistory,
            { role: "user", content: user_prompt }
        ];
        // 调试输出prompt内容
        console.log("[AI DEBUG] user_prompt:", user_prompt);
        console.log("[AI DEBUG] messages:", JSON.stringify(messages, null, 2));
        // 3. 调用deepseek API
        try {
            const requestBody = {
                model: config.api.model,
                messages: messages,
                stream: false,
                response_format: config.api.response_format,
                max_tokens: config.api.max_tokens
            };
            console.log("[AI DEBUG] API Request Body:", JSON.stringify(requestBody, null, 2));
            const resp = await axios.post(
                config.api.base_url + "/v1/chat/completions",
                requestBody,
                {
                    headers: {
                        "Authorization": `Bearer ${config.api.key}`,
                        "Content-Type": "application/json"
                    }
                }
            );
            let content = resp.data.choices[0].message.content;
            console.log("[AI DEBUG] API Raw Response:", content);
            let json = {};
            try {
                json = JSON.parse(content);
            } catch {
                // 尝试提取JSON
                const match = content.match(/\{[\s\S]*\}/);
                if (match) {
                    json = JSON.parse(match[0]);
                } else {
                    json = { position: "A1", response: "AI解析失败，随机攻击！" };
                }
            }
            console.log("[AI DEBUG] Parsed JSON:", JSON.stringify(json));
            // 4. 解析坐标
            let pos = (json as any).position || "A1";
            let idx = this.aiPosToIndex(pos);
            console.log("[AI DEBUG] AI Chosen Position:", pos, "Index:", idx);
            if (idx < 0 || idx >= this.gridSize * this.gridSize) idx = 0;
            // 5. 记录对话历史
            this.aiPromptHistory.push({ role: "user", content: user_prompt });
            this.aiPromptHistory.push({ role: "assistant", content: content });
            this.aiRound++;
            // 6. 以AI身份执行playerTurn
            this.playerTurn({ sessionId: this.aiSessionId } as any, [idx]);
        } catch (e) {
            console.error("[AI DEBUG] AI deepseek API调用失败", e);
            // 失败时随机攻击
            let idx = 0;
            this.playerTurn({ sessionId: this.aiSessionId } as any, [idx]);
        }
    }
    aiPosToIndex(pos: string): number {
        // A1~J10转为0~99
        if (!/^[A-J](10|[1-9])$/.test(pos)) return 0;
        let col = pos.charCodeAt(0) - 65;
        let row = parseInt(pos.slice(1)) - 1;
        return row * this.gridSize + col;
    }
}