# MarketNest API Contract

> **Auto-generated** from OpenAPI specification on application startup (Development mode).
> Do NOT edit manually — changes will be overwritten. Add endpoints in code and restart the app.

**Version**: 1.0.0
**Title**: MarketNest API
**Description**: Multi-vendor marketplace REST API — browse, buy, sell, and manage orders.

## Table of Contents

- [MarketNest.Web](#marketnest.web)
- [TestRead](#testread)
- [TestWrite](#testwrite)

## MarketNest.Web

### `POST /api/set-language`


**Request Body**:

- Content-Type: `multipart/form-data`
- Content-Type: `application/x-www-form-urlencoded`

**Responses**:

| Status | Description |
|--------|-------------|
| `200` | OK |

---

## TestRead

### `GET /admin/tests`


**Parameters**:

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| `SearchName` | query | `string` | ❌ | — |
| `Page` | query | `integer, string` | ❌ | — |
| `PageSize` | query | `integer, string` | ❌ | — |
| `SortBy` | query | `string` | ❌ | — |
| `SortDesc` | query | `boolean` | ❌ | — |
| `Search` | query | `string` | ❌ | — |
| `Skip` | query | `integer, string` | ❌ | — |

**Responses**:

| Status | Description |
|--------|-------------|
| `200` | OK |

---

### `GET /admin/tests/{id}`

**Operation ID**: `GetTestById`

**Parameters**:

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| `id` | path | `string` | ✅ | — |

**Responses**:

| Status | Description |
|--------|-------------|
| `200` | OK |

---

## TestWrite

### `POST /admin/tests`


**Request Body**:

- Content-Type: `application/json`
  - Schema: `CreateTestRequest`
- Content-Type: `text/json`
  - Schema: `CreateTestRequest`
- Content-Type: `application/*+json`
  - Schema: `CreateTestRequest`

**Responses**:

| Status | Description |
|--------|-------------|
| `200` | OK |

---

### `PUT /admin/tests/{id}`


**Parameters**:

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| `id` | path | `string` | ✅ | — |

**Request Body**:

- Content-Type: `application/json`
  - Schema: `UpdateTestRequest`
- Content-Type: `text/json`
  - Schema: `UpdateTestRequest`
- Content-Type: `application/*+json`
  - Schema: `UpdateTestRequest`

**Responses**:

| Status | Description |
|--------|-------------|
| `200` | OK |

---

## Schemas

### `CreateTestRequest`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | `string` | ✅ | — |
| `valueCode` | `string` | ✅ | — |
| `valueAmount` | `number, string` | ✅ | — |
| `subTitles` | `null, array` | ❌ | — |

### `UpdateTestRequest`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | `string` | ✅ | — |
| `valueCode` | `string` | ✅ | — |
| `valueAmount` | `number, string` | ✅ | — |
| `subTitles` | `null, array` | ❌ | — |

