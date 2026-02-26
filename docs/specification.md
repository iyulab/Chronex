# Chronex Expression Specification v1.0

## 1. 개요

Chronex Expression은 표준 Unix cron 표현식의 **상위호환(superset)**이다.
기존 cron 표현식은 수정 없이 그대로 동작하며, 선택적 확장을 통해
타임존, 인터벌, 원샷(절대/상대), 지터, 스태거, 윈도우, 만료, 시작일, 최대 실행 횟수를 단일 문자열로 표현한다.

### 설계 원칙

1. **표준 호환**: 유효한 5필드/6필드 cron 표현식은 그대로 유효한 Chronex 표현식이다
2. **단일 문자열 완결**: 하나의 string으로 스케줄의 모든 조건을 표현한다
3. **결정론적 해석**: 동일한 표현식 + 동일한 기준 시각 → 항상 동일한 다음 실행 시각
4. **파서 분리 가능**: 확장 옵션은 `{}` 블록으로 분리되어 파서가 깔끔하게 처리 가능
5. **라운드트립 가능**: `Parse(expr).ToString()` → 원본과 의미적으로 동일한 문자열 복원

---

## 2. 정규 문법 (EBNF)

```ebnf
(* ===== Top-Level ===== *)

chronex_expression
    = [ timezone_prefix ] , schedule_body , [ options_block ] ;

(* ===== Timezone Prefix ===== *)

timezone_prefix
    = "TZ=" , iana_timezone , whitespace ;

iana_timezone
    = identifier , { "/" , identifier } ;
    (* 예: "Asia/Seoul", "America/New_York", "UTC" *)

(* ===== Schedule Body ===== *)
(* 상호 배타적: cron, alias, interval, once 중 정확히 하나 *)

schedule_body
    = cron_expression          (* 표준 cron *)
    | alias_expression         (* @daily, @hourly 등 *)
    | interval_expression      (* @every 30m *)
    | once_expression ;        (* @once 2025-03-01T09:00:00+09:00 *)

(* ----- Standard Cron ----- *)

cron_expression
    = cron_5field | cron_6field ;

cron_5field
    = minute_field , whitespace ,
      hour_field , whitespace ,
      dom_field , whitespace ,
      month_field , whitespace ,
      dow_field ;

cron_6field
    = second_field , whitespace ,
      minute_field , whitespace ,
      hour_field , whitespace ,
      dom_field , whitespace ,
      month_field , whitespace ,
      dow_field ;

(* ----- Cron Fields ----- *)

second_field  = cron_field ;   (* 0-59 *)
minute_field  = cron_field ;   (* 0-59 *)
hour_field    = cron_field ;   (* 0-23 *)
dom_field     = dom_cron_field ;  (* 1-31, with L/W support *)
month_field   = month_cron_field ;  (* 1-12 or JAN-DEC *)
dow_field     = dow_cron_field ;  (* 0-7, SUN-SAT, with L/# support *)

cron_field
    = "*"
    | cron_list ;

cron_list
    = cron_element , { "," , cron_element } ;

cron_element
    = cron_value , [ "-" , cron_value ] , [ "/" , positive_integer ]
    | "*" , "/" , positive_integer ;

cron_value
    = integer ;

(* Day-of-Month: 추가로 L, W 지원 *)
dom_cron_field
    = cron_field
    | "L"                     (* 말일 *)
    | "LW"                    (* 말일에 가장 가까운 평일 *)
    | "L-" , positive_integer (* 말일에서 N일 전 *)
    | integer , "W" ;         (* N일에 가장 가까운 평일 *)

(* Month: 숫자 또는 이름 *)
month_cron_field
    = cron_field
    | month_name_list ;

month_name_list
    = month_name_element , { "," , month_name_element } ;

month_name_element
    = month_name , [ "-" , month_name ] , [ "/" , positive_integer ] ;

month_name
    = "JAN" | "FEB" | "MAR" | "APR" | "MAY" | "JUN"
    | "JUL" | "AUG" | "SEP" | "OCT" | "NOV" | "DEC" ;

(* Day-of-Week: 숫자/이름 + L/# 지원 *)
dow_cron_field
    = cron_field
    | dow_name_list
    | dow_name , "L"              (* 해당 요일의 마지막 주 *)
    | dow_name , "#" , digit ;    (* 해당 요일의 N번째 주 *)

dow_name_list
    = dow_name_element , { "," , dow_name_element } ;

dow_name_element
    = dow_name , [ "-" , dow_name ] , [ "/" , positive_integer ] ;

dow_name
    = "SUN" | "MON" | "TUE" | "WED" | "THU" | "FRI" | "SAT" ;

(* ----- Aliases ----- *)

alias_expression
    = "@yearly" | "@annually"     (* = 0 0 1 1 * *)
    | "@monthly"                  (* = 0 0 1 * * *)
    | "@weekly"                   (* = 0 0 * * 0 *)
    | "@daily" | "@midnight"      (* = 0 0 * * * *)
    | "@hourly" ;                 (* = 0 * * * * *)

(* ----- Interval ----- *)

interval_expression
    = "@every" , whitespace , duration
    | "@every" , whitespace , duration , "-" , duration ;
    (* "@every 30m"      → 고정 30분 간격 *)
    (* "@every 1h-2h"    → 1~2시간 랜덤 간격 *)

(* ----- Once ----- *)

once_expression
    = "@once" , whitespace , iso8601_datetime
    | "@once" , whitespace , relative_duration ;
    (* "@once 2025-03-01T09:00:00+09:00" — 절대 시각 *)
    (* "@once +20m" — 기준 시각으로부터 상대 시간 *)

relative_duration
    = "+" , duration ;
    (* "+20m", "+1h30m", "+2h" *)


(* ===== Options Block ===== *)

options_block
    = whitespace , "{" , option_list , "}" ;

option_list
    = option , { "," , option } ;

option
    = whitespace_opt , option_key , ":" , whitespace_opt , option_value , whitespace_opt ;

option_key
    = "jitter"       (* 비결정론적 랜덤 지연: duration *)
    | "stagger"      (* 결정론적 고정 오프셋: duration — 동시 실행 분산 *)
    | "window"       (* 실행 윈도우: duration *)
    | "from"         (* 시작일: ISO 8601 date 또는 datetime *)
    | "until"        (* 만료일: ISO 8601 date 또는 datetime *)
    | "max"          (* 최대 실행 횟수: positive integer *)
    | "tag" ;        (* 태그: 임의 문자열, 쉼표 구분은 +로 연결 *)

option_value
    = duration                (* jitter, window *)
    | iso8601_date            (* from, until — date only *)
    | iso8601_datetime        (* from, until — with time *)
    | positive_integer        (* max *)
    | tag_value ;             (* tag *)

tag_value
    = identifier , { "+" , identifier } ;
    (* 예: "report+daily" → ["report", "daily"] *)


(* ===== Primitives ===== *)

duration
    = positive_integer , duration_unit , { positive_integer , duration_unit } ;
    (* 복합 duration: "1h30m", "2h", "30s", "500ms" *)

duration_unit
    = "ms"      (* 밀리초 *)
    | "s"       (* 초 *)
    | "m"       (* 분 *)
    | "h"       (* 시 *)
    | "d" ;     (* 일 *)

iso8601_datetime
    = date_part , "T" , time_part , [ timezone_offset ] ;

iso8601_date
    = date_part ;

date_part
    = year , "-" , month_num , "-" , day_num ;

time_part
    = hour_num , ":" , minute_num , ":" , second_num ;

timezone_offset
    = "Z"
    | ( "+" | "-" ) , hour_num , ":" , minute_num ;

year       = digit , digit , digit , digit ;
month_num  = digit , digit ;
day_num    = digit , digit ;
hour_num   = digit , digit ;
minute_num = digit , digit ;
second_num = digit , digit ;

positive_integer = digit_nonzero , { digit } | "0" ;
integer          = [ "-" ] , positive_integer ;
identifier       = letter , { letter | digit | "_" | "-" } ;
whitespace       = " " , { " " } ;
whitespace_opt   = { " " } ;
digit            = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
digit_nonzero    = "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
letter           = "A"-"Z" | "a"-"z" ;
```

---

## 3. 의미론 (Semantics)

### 3.1 5필드 vs 6필드 판별

파서는 공백으로 분리된 필드 수로 5필드(분~요일)와 6필드(초~요일)를 구분한다.
Options block `{...}`이 있는 경우, 그 앞까지의 필드만 카운트한다.

| 필드 수 | 해석 |
|---------|------|
| 5 | `minute hour dom month dow` |
| 6 | `second minute hour dom month dow` |

5필드 표현식의 초(second)는 암묵적으로 `0`이다.

### 3.2 값 범위

| 필드 | 범위 | 특수 문자 |
|------|------|-----------|
| second | 0-59 | `*` `,` `-` `/` |
| minute | 0-59 | `*` `,` `-` `/` |
| hour | 0-23 | `*` `,` `-` `/` |
| day of month | 1-31 | `*` `,` `-` `/` `L` `W` |
| month | 1-12 또는 JAN-DEC | `*` `,` `-` `/` |
| day of week | 0-7 (0,7=SUN) 또는 SUN-SAT | `*` `,` `-` `/` `L` `#` |

### 3.3 역순 범위 (Reversed Range)

`23-01`은 `23,00,01`과 동일하다. `FRI-MON`은 `FRI,SAT,SUN,MON`과 동일하다.
이를 통해 자정을 넘나드는 시간 범위나 주말을 포함한 요일 범위를 자연스럽게 표현한다.

### 3.4 Timezone (`TZ=`)

`TZ=` 접두사가 있으면 해당 IANA 타임존에서 표현식을 해석한다.
`TZ=` 접두사가 없으면 호출자가 제공하는 타임존 (또는 UTC)에서 해석한다.

표현식 내 타임존은 다음 실행 시각 계산, DST 전환 처리, 결과 시각의 오프셋 결정에 모두 영향을 미친다.

### 3.5 DST 처리 규칙

Vixie Cron의 의미론을 따른다:

**Spring Forward (시계가 앞으로):**
- 무효한 시간에 매핑되는 non-interval 표현식 → 다음 유효 시간으로 조정
- interval 표현식 → occurrence를 건너뛰지 않음 (조정된 시간에 실행)

**Fall Back (시계가 뒤로):**
- non-interval 표현식 → 첫 번째 occurrence만 실행 (중복 방지)
- interval 표현식 → 양쪽 모두 실행 (건너뛰지 않음)

**Non-interval 표현식의 정의:** second, minute, hour 필드 중 어떤 것도 `*`, 범위, 또는 step을 포함하지 않는 표현식.
예: `0 30 1 * * *` (non-interval), `*/5 * * * *` (interval)

### 3.6 `@every` (Interval)

`@every <duration>`은 고정 간격 실행이다. Cron과 달리 calendar-aligned가 아니다.
- 첫 실행은 스케줄러 시작 시점 + duration 이후
- 이후 실행은 이전 **스케줄된 시각**(실제 실행 시각 아님) + duration

`@every <min>-<max>`는 각 간격이 min~max 사이의 균등분포 랜덤 값이다.
jitter와 달리, 간격 자체가 매번 달라진다.

### 3.7 `@once` (One-shot)

`@once`는 정확히 1회 실행되는 스케줄이다. 두 가지 형식을 지원한다:

**절대 시각:** `@once <iso8601>` — 지정된 시각에 실행된다.
지정된 시각이 이미 과거이면 `GetNextOccurrence()`는 `null`을 반환한다.

**상대 시간:** `@once +<duration>` — 기준 시각(reference time)으로부터 duration 이후에 실행된다.

```
@once +20m         → 기준 시각 + 20분
@once +1h30m       → 기준 시각 + 1시간 30분
@once +2h          → 기준 시각 + 2시간
```

상대 시간은 파싱 시점에 절대 시각으로 변환된다. 결정론성을 유지하기 위해 `Parse`에 기준 시각을 명시해야 한다:

```csharp
// 기준 시각 명시 — 결정론적
var expr = ChronexExpression.Parse("@once +20m", referenceTime: DateTimeOffset.UtcNow);
expr.OnceAt  // → referenceTime + 20분의 절대 시각

// 기준 시각 생략 — Parse 호출 시점의 UTC Now가 기준
var expr = ChronexExpression.Parse("@once +20m");
```

변환 후에는 절대 시각 `@once`와 동일하게 동작한다. `ToString()`은 변환된 절대 시각을 반환한다.

### 3.8 Options Block `{...}`

Options block은 schedule body 뒤에 공백으로 분리되어 나타난다.
`{}`로 감싸고, 쉼표로 구분된 key:value 쌍들을 포함한다.

| 옵션 | 타입 | 의미 | 기본값 |
|------|------|------|--------|
| `jitter` | duration | 각 실행에 `[0, jitter)` 범위의 랜덤 지연을 추가 (비결정론적) | 없음 (0) |
| `stagger` | duration | 트리거 ID 기반의 고정 오프셋 `[0, stagger)` (결정론적) | 없음 (0) |
| `window` | duration | 스케줄된 시각부터 window 시간 내에만 실행 허용. window를 넘기면 skip. | 무제한 |
| `from` | date/datetime | 이 시각 이전의 occurrence는 무시 | 없음 |
| `until` | date/datetime | 이 시각 이후의 occurrence는 무시 | 없음 |
| `max` | integer | 총 실행 횟수 제한. max 도달 후 occurrence 없음 | 무제한 |
| `tag` | string | 메타데이터 태그. 실행 로직에 영향 없음. 복수는 `+`로 연결 | 없음 |

**`from`/`until`이 date only인 경우:**
- `from:2025-06-01` → `2025-06-01T00:00:00` (해당 타임존의 자정)
- `until:2025-12-31` → `2025-12-31T23:59:59.999` (해당 타임존의 마지막 순간)

**`jitter`와 `stagger`의 차이:**

| | `jitter` | `stagger` |
|-|----------|-----------|
| 성격 | 비결정론적 (매 실행 랜덤) | 결정론적 (등록 시 고정) |
| 오프셋 계산 | 실행마다 `[0, jitter)` 범위의 새 랜덤 값 | `hash(triggerId) % stagger` 고정 값 |
| 용도 | 부하 분산 (외부 API 호출 등) | 동시 실행 충돌 방지 (top-of-hour 문제) |
| 예측 가능성 | 불가 | 트리거 ID가 같으면 항상 동일한 오프셋 |

두 옵션을 함께 사용할 수 있다. 적용 순서: `scheduled_time + stagger_offset + jitter_random`.

**`jitter`/`stagger`와 `window`의 관계:**
jitter + stagger가 적용된 후의 시각이 window를 초과하면 해당 occurrence는 skip된다.

**`max`의 카운팅:**
skip된 occurrence는 카운트하지 않는다. 실제 실행(또는 실행 시도)된 횟수만 카운트한다.

### 3.9 Alias 확장

| Alias | 동등한 Cron |
|-------|------------|
| `@yearly`, `@annually` | `0 0 1 1 *` |
| `@monthly` | `0 0 1 * *` |
| `@weekly` | `0 0 * * 0` |
| `@daily`, `@midnight` | `0 0 * * *` |
| `@hourly` | `0 * * * *` |

Alias에도 TZ 접두사와 options block을 붙일 수 있다:
`TZ=Asia/Seoul @daily {jitter:5m}`

---

## 4. 표현식 예시

### 기본 (표준 cron 호환)

```
*/5 * * * *                              # 매 5분
0 9 * * MON-FRI                          # 평일 09:00
0 0 1 * *                                # 매월 1일 자정
30 4 1,15 * *                            # 매월 1, 15일 04:30
0 22 * * 1-5                             # 평일 22:00
```

### 6필드 (초 포함)

```
0 */5 * * * *                            # 매 5분 0초
30 0 * * * *                             # 매 시간 0분 30초
*/10 * * * * *                           # 매 10초
```

### 타임존

```
TZ=Asia/Seoul 0 9 * * *                  # 서울 시간 매일 09:00
TZ=America/New_York 0 17 * * MON-FRI     # 뉴욕 시간 평일 17:00
TZ=UTC 0 0 * * *                         # UTC 자정
```

### 확장 문자

```
0 0 L * *                                # 매월 말일 자정
0 0 LW * *                               # 매월 말일에 가장 가까운 평일
0 0 L-3 * *                              # 매월 말일에서 3일 전
0 0 15W * *                              # 매월 15일에 가장 가까운 평일
0 0 * * 5L                               # 매월 마지막 금요일
0 0 * * MON#2                            # 매월 두 번째 월요일
```

### Interval

```
@every 30m                               # 30분마다
@every 2h                                # 2시간마다
@every 1h30m                             # 1시간 30분마다
@every 45s                               # 45초마다
@every 1h-2h                             # 1~2시간 랜덤 간격
```

### One-shot

```
@once 2025-03-01T09:00:00+09:00          # 특정 시각 1회 (절대)
@once 2025-12-31T23:59:59Z               # UTC 기준 (절대)
@once +20m                               # 20분 후 (상대)
@once +1h30m                             # 1시간 30분 후 (상대)
```

### Alias

```
@daily                                   # 매일 자정
@hourly                                  # 매시 정각
@weekly                                  # 매주 일요일 자정
```

### Options

```
0 9 * * * {jitter:30s}                   # 매일 09:00 + 0~30초 랜덤 (비결정론적)
0 * * * * {stagger:5m}                   # 매시 정각 + 고정 오프셋 0~5분 (결정론적)
0 9 * * * {stagger:3m, jitter:10s}       # stagger + jitter 함께 사용
0 9 * * * {window:15m}                   # 09:00~09:15 안에만 실행
*/10 * * * * {until:2025-12-31}          # 2025년 말까지만
@every 1h {max:10}                       # 최대 10회
@every 5m {from:2025-06-01, until:2025-12-31}  # 기간 한정
```

### 복합

```
TZ=Asia/Seoul 0 9 * * MON-FRI {jitter:30s, until:2025-12-31}
TZ=UTC @every 15m {window:5m, max:100, tag:health-check}
TZ=America/Chicago 0 0 L * * {jitter:1m, tag:monthly+report}
0 * * * * {stagger:5m, tag:hourly+batch}
@once 2025-04-01T02:00:00+09:00 {tag:migration}
@once +30m {tag:reminder}
```

---

## 5. 검증 규칙

파서는 다음 규칙을 적용하고, 위반 시 구조화된 에러 목록을 반환한다.

### 5.1 필드 범위 검증

| 규칙 ID | 검증 | 에러 메시지 |
|---------|------|-----------|
| `E001` | second ∈ [0, 59] | `second: value {v} out of range [0, 59]` |
| `E002` | minute ∈ [0, 59] | `minute: value {v} out of range [0, 59]` |
| `E003` | hour ∈ [0, 23] | `hour: value {v} out of range [0, 23]` |
| `E004` | day of month ∈ [1, 31] | `dayOfMonth: value {v} out of range [1, 31]` |
| `E005` | month ∈ [1, 12] | `month: value {v} out of range [1, 12]` |
| `E006` | day of week ∈ [0, 7] | `dayOfWeek: value {v} out of range [0, 7]` |
| `E007` | step > 0 | `{field}: step must be positive, got {v}` |

### 5.2 구조 검증

| 규칙 ID | 검증 | 에러 메시지 |
|---------|------|-----------|
| `E010` | 필드 수가 5 또는 6 (cron) | `expected 5 or 6 fields, got {n}` |
| `E011` | TZ가 유효한 IANA timezone | `timezone: unknown timezone '{tz}'` |
| `E012` | @once datetime이 유효한 ISO 8601 | `once: invalid datetime format '{v}'` |
| `E013` | @every duration이 양수 | `every: duration must be positive` |
| `E014` | @every range에서 min < max | `every: min duration must be less than max` |
| `E015` | 알 수 없는 option key | `options: unknown option '{key}'` |
| `E016` | option value 타입 불일치 | `options.{key}: expected {type}, got '{v}'` |
| `E017` | @once 상대 시간의 duration이 양수 | `once: relative duration must be positive` |

### 5.3 논리 검증

| 규칙 ID | 검증 | 에러 메시지 |
|---------|------|-----------|
| `E020` | from < until | `options: 'from' must be before 'until'` |
| `E021` | max > 0 | `options.max: must be positive, got {v}` |
| `E022` | jitter < 스케줄 최소 간격의 50% | `options.jitter: {v} exceeds 50% of schedule interval` (경고) |
| `E023` | window > 0 | `options.window: must be positive` |
| `E024` | stagger > 0 | `options.stagger: must be positive` |
| `E025` | stagger < 스케줄 최소 간격 | `options.stagger: {v} exceeds schedule interval` (경고) |

### 5.4 검증 결과 구조

```
ValidationResult:
  IsValid: bool
  Errors: ValidationError[]
  Warnings: ValidationWarning[]

ValidationError:
  Code: string         # "E001"
  Field: string        # "hour"
  Message: string      # "value 25 out of range [0, 23]"
  Value: string        # "25"
  Position: int?       # 표현식 내 문자 위치 (선택)

ValidationWarning:
  Code: string
  Field: string
  Message: string
```

---

## 6. 직렬화/역직렬화

### 6.1 정규화 (Canonical Form)

`ToString()`은 다음 순서로 정규화된 문자열을 반환한다:

```
[TZ=<timezone> ]<schedule>[ {<options>}]
```

- 타임존이 설정되어 있으면 `TZ=` 접두사로 시작
- Options는 키 알파벳 순으로 정렬
- Duration은 가장 큰 단위부터 (예: `1h30m`, `90m` 아님)
- 불필요한 공백 제거

### 6.2 동등성

두 표현식이 **의미적으로 동등**하려면:
- 정규화된 schedule body가 동일
- 타임존이 동일 (없음 = 없음)
- 모든 options의 key-value가 동일

`0 0 * * 0`과 `@weekly`는 의미적으로 동등하다.
`0 0 * * 7`과 `0 0 * * 0`은 의미적으로 동등하다 (둘 다 일요일).

---

## 7. JSON Schema 참고

### 7.1 TriggerDefinition Schema

`TriggerDefinition`은 직렬화 가능한 트리거 정의다. 런타임 핸들러(delegate)를 포함하지 않으며,
외부 시스템(CLI, API, 설정 파일 등)이 생성하고 소비 앱이 핸들러를 바인딩하는 패턴을 지원한다.

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Chronex TriggerDefinition",
  "type": "object",
  "required": ["id", "expression"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-zA-Z0-9_-]+$",
      "description": "Unique trigger identifier"
    },
    "expression": {
      "type": "string",
      "description": "Chronex expression string",
      "examples": [
        "*/5 * * * *",
        "TZ=Asia/Seoul 0 9 * * MON-FRI {jitter:30s}",
        "0 * * * * {stagger:5m}",
        "@every 15m {max:100}",
        "@once 2025-03-01T09:00:00+09:00",
        "@once +20m"
      ]
    },
    "enabled": {
      "type": "boolean",
      "default": true
    },
    "metadata": {
      "type": "object",
      "additionalProperties": { "type": "string" },
      "description": "Free-form key-value pairs passed through to TriggerContext"
    }
  }
}
```

### 7.2 TriggerDefinition 사용 패턴

**외부 시스템이 JSON으로 트리거 정의를 생성:**

```json
{
  "id": "health-check",
  "expression": "TZ=UTC @every 15m {stagger:3m}",
  "enabled": true,
  "metadata": {
    "endpoint": "https://api.example.com/health",
    "env": "prod",
    "delivery.mode": "webhook",
    "delivery.to": "https://hooks.example.com/results"
  }
}
```

**소비 앱이 핸들러를 바인딩:**

```csharp
var definition = JsonSerializer.Deserialize<TriggerDefinition>(json);
scheduler.Register(definition, async (ctx, ct) =>
{
    // ctx.Metadata contains all keys from the definition
    var endpoint = ctx.Metadata["endpoint"];
    await HealthCheckAsync(endpoint, ct);
});
```

이 패턴에서 Chronex가 보장하는 것:
1. 표현식이 단일 문자열 — 프로그래매틱 생성과 검증이 쉬움
2. `Validate()`가 구조화된 에러 반환 — 호출자가 잘못된 표현식을 자가 수정 가능
3. 메타데이터가 자유형 — 소비 앱이 필요한 모든 컨텍스트를 실어 보냄

### 7.3 Metadata 컨벤션

Chronex는 metadata 키를 해석하지 않는다. 다음은 소비 앱 간 상호운용을 위한 권장 컨벤션이다:

| 키 | 용도 | 예시 |
|----|------|------|
| `env` | 환경 태그 | `"prod"`, `"staging"` |
| `endpoint` | 대상 API 엔드포인트 | `"https://api.example.com"` |
| `scope` | 실행 격리 힌트 | `"isolated"`, `"shared"` |
| `scope.session` | 세션 키 | `"cron:health-check"` |
| `delivery.mode` | 결과 전달 방식 | `"webhook"`, `"queue"`, `"none"` |
| `delivery.to` | 결과 전달 대상 | `"https://hooks.example.com/results"` |
| `delivery.channel` | 알림 채널 | `"slack"`, `"email"` |

이 키들은 강제 사항이 아닌 권장 사항이다. 소비 앱은 자유롭게 자체 키를 정의할 수 있다.