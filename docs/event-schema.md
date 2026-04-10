# Event Schema Specification

This document defines the structure of events emitted by OgmiosDotnet.BlockchainEvents. All events conform to the [CloudEvents 1.0 specification](https://cloudevents.io/) with Cardano-specific extensions.

Events are delivered identically via two protocols:
- **HTTP** — JSON/CloudEvents via Dapr pub/sub (port 4000)
- **gRPC** — Protobuf via server-side streaming (port 4010, see `protos/blockchain_events.proto`)

The payload structure is the same regardless of delivery protocol.

## CloudEvents Envelope

Every emitted event follows the CloudEvents 1.0 structured content mode:

```json
{
  "specversion": "1.0",
  "id": "<unique-event-id>",
  "source": "<event-source-uri>",
  "type": "<event-type>",
  "subject": "<rule-name>",
  "time": "<rfc3339-timestamp>",
  "datacontenttype": "application/json",
  "dataschema": "https://schema.cardano.org/events/transaction-matched/v1",
  "data": { ... }
}
```

### Required Attributes

| Attribute         | Type      | Description                        | Example                                                |
| ----------------- | --------- | ---------------------------------- | ------------------------------------------------------ |
| `specversion`     | String    | CloudEvents version (always "1.0") | `"1.0"`                                                |
| `id`              | String    | Unique event identifier            | `"tx-abc123-address-match-1705312200000"`              |
| `source`          | URI       | Event origin context               | `"cardano://mainnet/slot/115545883/block/4e58bb36..."` |
| `type`            | String    | Event type descriptor              | `"io.cardano.transaction.address-match"`               |
| `time`            | Timestamp | Event timestamp (RFC 3339)         | `"2024-01-15T10:30:00.000Z"`                           |
| `datacontenttype` | String    | Payload content type               | `"application/json"`                                   |
| `data`            | Object    | Event payload                      | See below                                              |

### Optional Attributes

| Attribute    | Type   | Description            | Example                                   |
| ------------ | ------ | ---------------------- | ----------------------------------------- |
| `subject`    | String | Rule name that matched | `"Address Match"`                         |
| `dataschema` | URI    | Schema reference       | `"https://schema.cardano.org/events/..."` |

## Cardano Extension Attributes

These extension attributes provide Cardano-specific context:

| Attribute            | Type    | Description          | Example                               |
| -------------------- | ------- | -------------------- | ------------------------------------- |
| `cardanoslot`        | Integer | Absolute slot number | `115545883`                           |
| `cardanoblock`       | String  | Block hash (hex)     | `"4e58bb36837b32f894c8a57..."`        |
| `cardanoblockheight` | Integer | Block height         | `10842567`                            |
| `cardanoera`         | String  | Cardano era name     | `"Conway"`                            |
| `cardanonetwork`     | String  | Network identifier   | `"mainnet"`, `"preprod"`, `"preview"` |

## Event Data Payload

The `data` field contains the matched transaction details:

```json
{
  "transactionId": "abc123def456...",
  "slot": 115545883,
  "blockHeight": 10842567,
  "blockHash": "4e58bb36837b32f894c8a57...",
  "ruleId": "address-match",
  "ruleName": "Address Match",
  "matchedCriteria": {
    "matched_addresses": ["addr1qx..."]
  },
  "transaction": {
    "id": "abc123def456...",
    "slot": 115545883,
    "blockHash": "4e58bb36837b32f894c8a57...",
    "blockHeight": 10842567,
    "index": 0,
    "fee": 200000,
    "inputAddresses": ["addr1qx...", "addr1qy..."],
    "outputAddresses": ["addr1qz...", "addr1qw..."],
    "mintedAssets": {},
    "metadata": {},
    "hasGovernanceAction": false,
    "hasTreasuryWithdrawal": false,
    "hasStakeDelegation": false,
    "hasStakeRegistration": false,
    "hasVote": false
  }
}
```

### TransactionMatchedData Schema

| Field             | Type            | Required | Description                         |
| ----------------- | --------------- | -------- | ----------------------------------- |
| `transactionId`   | String          | Yes      | Transaction hash                    |
| `slot`            | Integer         | Yes      | Slot where transaction was included |
| `blockHeight`     | Integer         | Yes      | Block height                        |
| `blockHash`       | String          | Yes      | Block hash                          |
| `ruleId`          | String          | Yes      | ID of the matching rule             |
| `ruleName`        | String          | Yes      | Human-readable rule name            |
| `matchedCriteria` | Object          | Yes      | Rule-specific match details         |
| `transaction`     | TransactionData | Yes      | Full transaction data               |

### TransactionData Schema

| Field                   | Type     | Required | Description                       |
| ----------------------- | -------- | -------- | --------------------------------- |
| `id`                    | String   | Yes      | Transaction hash                  |
| `slot`                  | Integer  | Yes      | Slot number                       |
| `blockHash`             | String   | Yes      | Parent block hash                 |
| `blockHeight`           | Integer  | Yes      | Parent block height               |
| `index`                 | Integer  | Yes      | Index within block                |
| `fee`                   | Integer  | No       | Fee in lovelace (0 for Byron)     |
| `inputAddresses`        | String[] | No       | Input addresses                   |
| `outputAddresses`       | String[] | No       | Output addresses                  |
| `mintedAssets`          | Object   | No       | Minted/burned assets by policy ID |
| `metadata`              | Object   | No       | Transaction metadata by label     |
| `hasGovernanceAction`   | Boolean  | No       | Contains governance proposal      |
| `hasTreasuryWithdrawal` | Boolean  | No       | Contains treasury withdrawal      |
| `hasStakeDelegation`    | Boolean  | No       | Contains stake delegation         |
| `hasStakeRegistration`  | Boolean  | No       | Contains stake registration       |
| `hasVote`               | Boolean  | No       | Contains vote                     |

The `matchedCriteria` field contains rule-specific match details in JSON format. The structure varies depending on which rule matched the transaction.

## Complete Event Example

```json
{
  "specversion": "1.0",
  "id": "tx-8a3b2c1d-address-match-1705312200000",
  "source": "cardano://mainnet/slot/115545883/block/4e58bb36837b32f894c8a57006e24b64c2d77bf4fc13b3b2c428fee8871e2491",
  "type": "io.cardano.transaction.address-match",
  "subject": "Address Match",
  "time": "2024-01-15T10:30:00.000Z",
  "datacontenttype": "application/json",
  "dataschema": "https://schema.cardano.org/events/transaction-matched/v1",
  "cardanoslot": 115545883,
  "cardanoblock": "4e58bb36837b32f894c8a57006e24b64c2d77bf4fc13b3b2c428fee8871e2491",
  "cardanoblockheight": 10842567,
  "cardanoera": "Conway",
  "cardanonetwork": "mainnet",
  "data": {
    "transactionId": "8a3b2c1d4e5f6789abcdef0123456789abcdef0123456789abcdef0123456789",
    "slot": 115545883,
    "blockHeight": 10842567,
    "blockHash": "4e58bb36837b32f894c8a57006e24b64c2d77bf4fc13b3b2c428fee8871e2491",
    "ruleId": "address-match",
    "ruleName": "Address Match",
    "matchedCriteria": {
      "matched_addresses": ["addr1qx2fxv2umyhttkxyxp8x0dlpdt3k6cwng5pxj3jhsydzer3jcu5d8ps7zex2k2xt3uqxgjqnnj83ws8lhrn648jjxtwq2ytjqp"]
    },
    "transaction": {
      "id": "8a3b2c1d4e5f6789abcdef0123456789abcdef0123456789abcdef0123456789",
      "slot": 115545883,
      "blockHash": "4e58bb36837b32f894c8a57006e24b64c2d77bf4fc13b3b2c428fee8871e2491",
      "blockHeight": 10842567,
      "index": 5,
      "fee": 180000,
      "inputAddresses": ["addr1qy3q0qy54y3gxf9ryk85vjzk7k8dyvk6rz57vh5kg7j2lpzpu4rwa8tu3euqlp38kcylwhtfs5kmfnvfkl8atp0tzuhsft7k0j"],
      "outputAddresses": ["addr1qx2fxv2umyhttkxyxp8x0dlpdt3k6cwng5pxj3jhsydzer3jcu5d8ps7zex2k2xt3uqxgjqnnj83ws8lhrn648jjxtwq2ytjqp", "addr1qy3q0qy54y3gxf9ryk85vjzk7k8dyvk6rz57vh5kg7j2lpzpu4rwa8tu3euqlp38kcylwhtfs5kmfnvfkl8atp0tzuhsft7k0j"],
      "mintedAssets": {},
      "metadata": {},
      "hasGovernanceAction": false,
      "hasTreasuryWithdrawal": false,
      "hasStakeDelegation": false,
      "hasStakeRegistration": false,
      "hasVote": false
    }
  }
}
```

## Event Type Naming Convention

Event types follow the pattern:

```
io.cardano.transaction.<rule-id>
```

Examples:

- `io.cardano.transaction.address-match`
- `io.cardano.transaction.policy-id-asset`
- `io.cardano.transaction.metadata-key-value`
- `io.cardano.transaction.governance-treasury`
- `io.cardano.transaction.all-transactions`

## Event ID Generation

Event IDs are generated using the format:

```
{transactionId}-{ruleId}-{timestampMs}
```

This ensures:

- Uniqueness across all events
- Traceability back to source transaction
- Temporal ordering via timestamp

## Source URI Format

The `source` URI follows the pattern:

```
cardano://{network}/slot/{slot}/block/{blockHash}
```

Examples:

- `cardano://mainnet/slot/115545883/block/4e58bb36...`
- `cardano://preprod/slot/50000000/block/abc123...`
- `cardano://preview/slot/1000000/block/def456...`

## Consuming Events

### Dapr Pub/Sub Subscription

```json
{
  "pubsubname": "pubsub",
  "topic": "blockchain-events",
  "route": "/events"
}
```

### HTTP Endpoint (SDK-less)

Events are delivered as HTTP POST requests with:

- Content-Type: `application/cloudevents+json`
- Body: CloudEvents JSON

### Filtering by Type

Consumers can filter events by the `type` attribute to receive only specific rule matches:

```python
if event['type'] == 'io.cardano.transaction.address-match':
    handle_address_match(event)
elif event['type'] == 'io.cardano.transaction.governance-treasury':
    handle_governance_event(event)
```
