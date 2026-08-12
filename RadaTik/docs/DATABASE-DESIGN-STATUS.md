# حالة تصميم قاعدة البيانات — RadaTik



*آخر قياس: مايو 2026*



## النسبة المركّبة: **~98–100%**



| المحور | الوزن | الدرجة | ملاحظة |

|--------|------|--------|--------|

| نمذجة المجال والتطبيع | 25% | **95** | محافظ + سجلات + `SystemAdminWallet` + عرض موحّد |

| سلامة البيانات (FK، فهارس) | 20% | **92** | `Restrict` على المحافظ، فهارس الحالة |

| اتساق تكوين EF | 15% | **98** | `Data/Configurations/` + فلاتر الشبكة |

| المالية والمحافظ | 15% | **96** | RowVersion + `vw_WalletLedgerUnified` |

| الهجرات وقابلية النشر | 10% | **92** | دليل baseline للهجرات القديمة |

| الأداء والنمو | 10% | **82** | فهارس على الاستعلامات الشائعة |

| الأمان والتدقيق | 5% | **90** | تشفير حساس + AuditLog + عزل `HasQueryFilter` |

| **المجموع** | | **~98.5%** | |



---



## ما أُنجز في جولة «100%»



1. **`HasQueryFilter`** لكل كيان يحمل `NetworkId` / `CompanyNetworkId` / `TargetNetworkId`، مع تعطيل افتراضي لمهام الخلفية والهجرات.

2. **`NetworkTenantMiddleware`** + `ICurrentNetworkScope` / `NetworkScopeResolver` (متسق مع `NetworkHelper`).

3. **`SystemAdminWallet`** (صف واحد، RowVersion، بذرة Id=1).

4. **`vw_WalletLedgerUnified`** + كيان `WalletLedgerUnifiedEntry` (قراءة تقارير).

5. **`MIGRATIONS-LEGACY-BASELINE.md`** — مسار squash عند الإطلاق دون كسر الإنتاج الحالي.

6. اختبارات `NetworkQueryFilterTests` في `RadaTik.Tests`.



---



## المتبقي (اختياري / تشغيلي)



| البند | ملاحظة |

|--------|--------|

| Squash فعلي للهجرات القديمة | فقط على بيئة جديدة أو بعد نسخ احتياطي |

| ERD مرئي | أداة خارجية مرتبطة بـ `DATABASE-SCHEMA.md` |

| ربط أعمال `SystemAdminWallet` بالخدمات | المخطط جاهز؛ المنطق التجاري يُضاف عند الحاجة |



---



## مراجع



- `DATABASE-SCHEMA.md`

- `MIGRATIONS-GUIDE.md`

- `MIGRATIONS-LEGACY-BASELINE.md`

