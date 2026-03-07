# Distributed Sensor System with Secure Communication

A distributed IoT sensor monitoring system built with .NET 10 that demonstrates secure client-server communication, failover mechanisms, and attack detection. This project simulates temperature sensors communicating with a central server via encrypted TCP connections.

> **Note**: This is an educational project for practicing distributed systems concepts and security principles.

## Features

### Core Functionality
- **Multi-Sensor Architecture**: 10 sensors (4 active, 6 standby) connected to a primary server
- **Real-time Monitoring**: Sensors send temperature readings at random intervals (1-20 seconds)
- **Database Logging**: PostgreSQL database stores sensor readings, variables, and alarms
- **Automatic Failover**: Active sensor failure triggers automatic promotion of standby sensors
- **Heartbeat Mechanism**: Sensors send periodic heartbeats (every 10 seconds) to prove they're alive
- **Alarm System**: Triggers alarms when sensor values exceed safe thresholds

### Security Features

#### 1. **Diffie-Hellman Key Exchange** 
Each sensor establishes a unique shared secret with the server using Elliptic Curve Diffie-Hellman (ECDH). This ensures secure key exchange even over an insecure network - no eavesdropper can derive the shared secret from intercepted public keys.

#### 2. **AES-256 Encryption**
All communication after the initial handshake is encrypted using AES-256-CBC. Each message uses a randomly generated Initialization Vector (IV) to prevent pattern analysis attacks.

#### 3. **HMAC-SHA256 Message Authentication**
Every sensor reading includes an HMAC signature computed from the message ID, sensor ID, value, and timestamp. The server validates this signature to detect tampering attempts. Additionally, the server tracks message IDs to prevent replay attacks.

### Attack Demonstration Features
For educational purposes, sensors support commands to simulate attacks:
- **`tamper`**: Modifies the temperature value after signature generation (detected by server)
- **`replay`**: Sends old message IDs to simulate replay attacks (detected by server)
- **`sleep`**: Freezes sensor for 50 seconds to simulate failure (triggers failover)

## Stack

- **.NET 10** - Application runtime
- **C# 14** - Programming language
- **Docker & Docker Compose** - Containerization and orchestration
- **PostgreSQL 15** - Database for logs and configuration
- **Serilog** - Structured logging
- **Npgsql** - PostgreSQL driver for .NET
- **System.Security.Cryptography** - Cryptographic operations

## 📋 Prerequisites

- [Docker](https://www.docker.com/get-started) and Docker Compose
- (Optional) PostgreSQL client like [pgAdmin](https://www.pgadmin.org/) or [DBeaver](https://dbeaver.io/) for database inspection

## 🚀 Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/naglic-a/DRUS-projekat
cd DRUS-projekat
```

### 2. Start the System
```bash
docker-compose up --build
```

This will start:
- PostgreSQL database (`drus_db`)
- Primary server (`drus_primary_server`)
- 10 sensor containers (`drus_sensor_1` through `drus_sensor_10`)

### 3. Initialize Database Variables

**IMPORTANT**: The server won't log data until you create variable definitions in the database.

Connect to the database:
```bash
docker exec -it drus_db psql -U admin -d drus_aleksa
```

Or use a PostgreSQL client with these credentials:
- **Host**: localhost
- **Port**: 5432
- **Database**: drus_aleksa
- **Username**: admin
- **Password**: admin

Run this SQL to create sensor variable definitions(doenst need to be exactly like this):
```sql
INSERT INTO drus_variables (name, description, unit, min_safe_value, max_safe_value)
VALUES 
    ('01', 'Temperature Sensor 1', '°C', 22.0, 80.0),
    ('02', 'Temperature Sensor 2', '°C', 22.0, 80.0),
    ('03', 'Temperature Sensor 3', '°C', 22.0, 80.0),
    ('04', 'Temperature Sensor 4', '°C', 22.0, 80.0),
    ('05', 'Temperature Sensor 5', '°C', 22.0, 80.0),
    ('06', 'Temperature Sensor 6', '°C', 22.0, 80.0),
    ('07', 'Temperature Sensor 7', '°C', 22.0, 80.0),
    ('08', 'Temperature Sensor 8', '°C', 22.0, 80.0),
    ('09', 'Temperature Sensor 9', '°C', 22.0, 80.0),
    ('10', 'Temperature Sensor 10', '°C', 22.0, 80.0);
```

Exit psql with `\q` or `exit`.

## Viewing the System in Action

### Examples of some commands
```bash
# Follow server logs in real-time
docker logs -f drus_primary_server

# View last 100 lines
docker logs --tail 100 drus_primary_server
```

### View Sensor Logs
```bash
# Follow sensor 1 logs
docker logs -f drus_sensor_1

# View logs from multiple sensors
docker-compose logs -f sensor-1 sensor-2 sensor-3
```

### Check All Running Containers
```bash
docker-compose ps
```

### View Database Logs
```sql
-- View recent sensor readings
SELECT v.name, l.var_value, l.log_timestamp, l.log_quality
FROM drus_log l
JOIN drus_variables v ON l.var_id = v.id
ORDER BY l.log_timestamp DESC
LIMIT 20;

-- View triggered alarms
SELECT v.name, a.alarm_type, a.triggered_value, a.message, a.alarm_timestamp
FROM drus_alarm a
JOIN drus_variables v ON a.var_id = v.id
ORDER BY a.alarm_timestamp DESC;

-- View sensor activity statistics
SELECT v.name, 
       COUNT(*) as reading_count,
       AVG(l.var_value) as avg_temperature,
       MIN(l.var_value) as min_temperature,
       MAX(l.var_value) as max_temperature
FROM drus_log l
JOIN drus_variables v ON l.var_id = v.id
GROUP BY v.name
ORDER BY v.name;
```

## Interactive Commands

### Attach to a Sensor
Attach to a running sensor to send commands:
```bash
docker attach drus_sensor_1
```

Once attached, you can type these commands:

- **`sleep`** - Freezes the sensor for 50 seconds (simulates sensor death, triggers failover)
- **`tamper`** - Toggles tampering mode (modifies data after signing)
- **`replay`** - Toggles replay attack mode (sends old message IDs)

**Note**: To detach without stopping the container, press `Ctrl+P` then `Ctrl+Q`. If you press `Ctrl+C`, the container will restart due to `restart: always`.

### Example Demonstration Flow

```bash
# Attach to an active sensor
docker attach drus_sensor_1

# Type: sleep
# Watch server logs to see failover happen
docker logs -f drus_primary_server
```

## Project Structure

```
DRUS_AleksaNaglic/
├── ServerApp/                  # Primary server application
│   ├── Program.cs             # Main server logic & sensor handling
│   └── ClusterManager.cs      # Failover, heartbeat tracking, attack detection
├── SensorApp/                  # Sensor application
│   └── Program.cs             # Sensor logic, readings, demo commands
├── SharedUtils/                # Shared libraries
│   ├── CryptoHelper.cs        # Diffie-Hellman key exchange (ECDH)
│   ├── AesEncryptionHelper.cs # AES-256 encryption/decryption
│   ├── DTOs/                  # Data Transfer Objects
│   │   ├── HandshakeRequestDto.cs
│   │   ├── HandshakeResponseDto.cs
│   │   └── SensorReadingDto.cs
│   └── Models/                # Database models
│       ├── DrusVariable.cs
│       ├── DrusLog.cs
│       └── DrusAlarm.cs
├── docker-compose.yml          # Container orchestration
├── Server.Dockerfile          # Server container definition
├── Sensor.Dockerfile          # Sensor container definition
└── init.sql                   # Database initialization script
```

## How It Works

### Connection Flow
1. **Sensor Startup**: Each sensor connects to the server via TCP
2. **Handshake**: Sensor and server exchange ECDH public keys to derive a shared secret
3. **Secure Communication**: All subsequent messages are encrypted with AES-256 using the shared secret
4. **Data Transmission**: 
   - Active sensors send temperature readings (with HMAC signature) every 1-20 seconds
   - All sensors send heartbeats every 10 seconds
5. **Server Processing**:
   - Validates HMAC signature (tamper detection)
   - Checks message ID sequence (replay detection)
   - Logs reading to database
   - Checks alarm thresholds

### Failover Mechanism
- **Watchdog**: Server runs a background task that checks sensor heartbeats every 5 seconds
- **Death Detection**: If an active sensor misses heartbeats for >30 seconds, it's marked as DEAD
- **Promotion**: Server automatically promotes a standby sensor to active status
- **Zombie Prevention**: If a "dead" sensor comes back to life and tries to send data, it's immediately forced to standby

### Attack Detection
- **Tampering**: Server recomputes HMAC signature and compares with received signature. Mismatches are logged and the message is dropped.
- **Replay**: Server tracks the highest message ID seen from each sensor. Messages with old IDs are detected and dropped.

## 🧹 Cleanup

Stop and remove all containers:
```bash
docker-compose down
```

Stop and remove containers + volumes (deletes database data):
```bash
docker-compose down -v
```

Remove images:
```bash
docker-compose down --rmi all
```

## 📝 Log Files

Logs are persisted to your local machine:
- Server logs: `./logs/server/`
- Sensor logs: `./logs/sensor-1/` through `./logs/sensor-10/`

## 🐛 Troubleshooting

**Problem**: Sensors connect but readings aren't logged to database
- **Solution**: Make sure you've inserted sensor variables into `drus_variables` table (see step 3 above)

**Problem**: "Address already in use" error
- **Solution**: Stop any service using port 8080 or 5432, or change ports in `docker-compose.yml`

**Problem**: Container won't stop after Ctrl+C when attached
- **Solution**: Use `Ctrl+P` then `Ctrl+Q` to detach, or run `docker stop <container_name>` from another terminal

## Author

Aleksa Naglić - Distributed Systems Course Project

## License

This project is for educational purposes as part of a university course.
