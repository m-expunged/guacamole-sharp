# GuacamoleSharp

GuacamoleSharp is a bare-bones C# replacement of the Apache Guacamole server-side Java servlet.
It is intended for customizable integration of Apache Guacamole into existing frontend projects with their own user and connection management.

Like the official Java guacamole-client, GuacamoleSharp makes use of the guacamole-protocol and is intended to be used with 'guacamole-common-js' (frontend client) and 'guacd' (server-side proxy) by Apache. Check out the [Apache Guacamole Docs](https://guacamole.apache.org/doc/gug/) for detailed information.

Inspired by Vadim Pronin's [guacamole-lite](https://github.com/vadimpronin/guacamole-lite).

# Usage

While not strictly required, the intended use of GuacamoleSharp is with Docker.

Docker setup steps:

- Clone repository
- Adjust appsettings.json (explanation below)
- In Dockerfile, overwrite appsettings.json as needed with environment variables (see Dockerfile for example)
- Adjust docker-compose.yml as needed
- In root directory of the project, use 'docker compose up' to generate images and start containers
- Generate connection token with settings (explanation below)
- Connect to GuacamoleSharp with your self-built frontend using guacamole-common-js (explanation below)

For usage without Docker, skip the Docker steps and build the application like any other.

# Connection Token

GuacamoleSharp transports the settings for remote connections via an encrypted connection token.

The token is given to the guacamole-common-js client as the 'token' query parameter while calling the 'connect' method.

```js
let tunnel = new Guacamole.WebSocketTunnel('ws://localhost:8080');
let client = new Guacamole.Client(tunnel);

client.connect(
  'token=hD12AB5Js4WD0Cse6mtgw_8msAieiSi1-vHajL2vAZgAo24yPufxuLKefeZxEYyWXhcbW21iv53Pv18gTXTnXp1i7wClkQ2tDutnIHqrHRo'
);
```

To generate a connection token you can use the GuacamoleSharp Web API.

Sending a POST request to 'HOSTNAME:PORT/api/token/TOKEN_ENCRYPTION_KEY' will return the token string.

The request requires two things:

The token encryption key defined in appsettings.json/Dockerfile inside the url:

    .../api/token/MySuperSecretCipherKey

And a connection object in the request body:

```json
{
  "settings": {
    "hostname": "127.0.0.1",
    "port": "22"
  },
  "type": "ssh"
}
```

Curl example:

```
curl -X 'POST' \
 'http://localhost:5072/api/Token/MySuperSecretCipherKey' \
 -H 'accept: text/plain' \
 -H 'Content-Type: application/json' \
 -d '{
"settings": {
"hostname": "127.0.0.1",
"port": "22"
},
"type": "ssh"
}'
```

If you start the application via Visual Studio in Debug mode, you can use Swagger to easily make the request.

# Appsettings.json

In order to simplify the configuration of connections, you can specify default and unencrypted settings for different connection types inside the appsettings.json.

## Default Guacd, WebSocket server and Token settings

By default, the GuacamoleSharp WebSocket server listens on port 8080 and tries to connect to guacd on port 4822.

If you want to specify different ports you can change them in the appsettings.json. If you are running GuacamoleSharp with Docker, you will also need to change the ports in the Dockerfile and docker-compose.yml.

The 'WebSocket' and 'Guacd' properties can be safely removed from the appsettings.json and will simply default 8080 and 4822 for ports and localhost for the WebSocket server IP.

In order to generate the required connection token, a encryption key has to be specified. This can be done in the appsettings.json or the Dockerfile.
This is the same key you use to generate a connection token.

```json
"Token": {
  "Password": "MySuperSecretCipherKey"
},
"WebSocket": {
  "Port": 8080
},
"Guacd": {
  "Hostname": "127.0.0.1",
  "Port": 4822
},
```

## Default connection settings

Since many connection settings will be the same across different connections, it might be convenient to define them as default values.

Any valid guacamole-protocol argument can be used. The most common settings are already preconfigured and will work out-of-the-box. You might want to change 'width' and 'height' to your preferred resolution.

The properties inside 'ConnectionDefaultSettings' are <strong>required to be in lowercase<strong> unless the guacamole-protocol specifies them otherwise.

```json
"Client": {
  "ConnectionDefaultSettings": {
    "rdp": {
      "args": "connect",
      "port": 3389,
      "width": 1024,
      "height": 768,
      "dpi": 96
    },
    "vnc": {
      "args": "connect",
      "port": 5900,
      "width": 1024,
      "height": 768,
      "dpi": 96
    },
    "ssh": {
      "args": "connect",
      "port": 22,
      "width": 1024,
      "height": 768,
      "dpi": 96,
      "server-alive-interval": 10
    },
    "telnet": {
      "args": "connect",
      "port": 23,
      "width": 1024,
      "height": 768,
      "dpi": 96
    }
  }
}
```

## Unencrypted connection settings

While most settings for connections should be packed into the encrypted token string, there is also the option to send certain settings as an unencrypted query parameter.

All valid guacamole-protocol arguments can used in their unencrypted form, with the exception of the connection type (ssh/rdp/...) which is the only strictly required argument inside the connection token.

The properties inside 'ConnectionAllowedUnencryptedSettings' are <strong>required to be in lowercase<strong> unless the guacamole-protocol specifies them otherwise.

```json
"ConnectionAllowedUnencryptedSettings": {
  "rdp": ["width", "height", "dpi"],
  "vnc": ["width", "height", "dpi"],
  "ssh": ["color-scheme", "font-name", "font-size", "width", "height", "dpi"],
  "telnet": ["color-scheme", "font-name", "font-size", "width", "height", "dpi"]
}
```

Example unencrypted settings usage:

```js
let tunnel = new Guacamole.WebSocketTunnel('ws://localhost:8080');
let client = new Guacamole.Client(tunnel);

let connectionString =
  'token=hD12AB5Js4WD0Cse6mtgw_8msAieiSi1-vHajL2vAZgAo24yPufxuLKefeZxEYyWXhcbW21iv53Pv18gTXTnXp1i7wClkQ2tDutnIHqrHRo';
connectionString += '&width=1024';
connectionString += '&height=768';

client.connect(connectionString);
```

## Settings overwrite priority

If a guacamole-protocol argument is defined multiple times, it will be overwritten by the setting with the highest priority.
The order of priority is as follows (from lowest to highest):

Default values < Token string values < Unencrypted values

# guacamole-common-js

In order to use GuacamoleSharp, you will need to use the guacamole-common-js library in your frontend. You can find a detailed explanation in the Apache Guacamole Docs.

A good way to get started is to look at the Angular example that is included in the repository, using the guacamole-common-js npm package. You might need to defined a d.ts file if you are using Typescript in your project.

If you want to include your own examples or notice something that could have been done better feel free to open a pull request.

```

```
