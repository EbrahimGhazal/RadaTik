# حالة التكامل — RadaTik (CompanyAdmin + الأدوار المرتبطة)

*آخر تحديث: مايو 2026*

## ملخص تنفيذي

| المحور | التقدير | ملاحظة |
|--------|---------|--------|
| المحور المالي (محافظ، صندوق، يومية، تسوية، فواتير) | **94–96%** | مركز مالي موحّد (`financial-hub-page`) على الصفحات الأساسية |
| الأدوار والصلاحيات | **90–92%** | CompanyAdmin، CollectionPoint (طلبات مشتركين)، CompanyEmployee، ClientPortal |
| المجالات التشغيلية (قطاعات، مستقبلات، مشتركين، طلبات) | **88–90%** | بطل تشغيلي (`_OperationalSysHero`) على الصفحات الرئيسية |
| الواجهات والتنقل | **92–94%** | `_FinancialQuickNav`، رصيد الهيدر/الشريط، مسارات named routes |
| **المجموع المرجّح** | **~92–94%** | المتبقي: صفحات CRUD ثانوية، محفظة مدير النظام، سجل موحّد |

الهدف **≥95%** على كل المحاور يتطلب إكمال صفحات البنية التحتية المتبقية + محفظة/أرشفة مدير النظام (اختياري للمنتج).

---

## 1. المحور المالي

| المكوّن | الحالة |
|---------|--------|
| مركز مالي سريع `_FinancialQuickNav` | ✓ |
| بطل مالي `_FinancialSysHero` | ✓ على 35+ صفحة CompanyAdmin |
| محفظة الشركة (Index, TopUp, Transactions) | ✓ |
| طلبات تغذية المشتركين (مدير شركة) | ✓ `ClientWalletTopUpRequests` |
| طلبات تغذية نقاط التحصيل | ✓ `CollectionPoints/TopUpRequests` |
| نقطة تحصيل — طلبات مشتركين | ✓ مسار `collectionPoint-wallet-client-topups` + قائمة جانبية |
| صندوق / يومية / تسوية / أعمال الشركة | ✓ |
| مستودع / جرد / فواتير مواد | ✓ |
| رواتب / صيانة / تركيب مشترك | ✓ |
| تقارير (Index, Templates, Edit) | ✓ |

**فجوات:** `SystemAdminWallet` + أرشفة شهرية؛ سجل معاملات موحّد عبر الأدوار؛ بعض صفحات التفاصيل الفرعية للفواتير.

---

## 2. الأدوار

| الدور | التكامل | أبرز الروابط |
|-------|---------|--------------|
| مدير الشركة (`NetworkAdministrator`) | ~95% | مركز مالي، محفظة، موافقة طلبات مشتركين |
| موظف الشركة (`CompanyEmployee`) | ~90% | محفظة Dashboard، هيدر رصيد |
| نقطة التحصيل (`CollectionPoint`) | ~92% | تغذية محفظة، قبض، صندوق، طلبات مشتركين |
| العميل (`ClientPortal`) | ~85% | رصيد وطلبات — بدون هيدر محفظة موحّد بعد |
| مدير النظام (`SystemAdministrator`) | ~80% | `FundingRequests` — بدون كيان محفظة مستقل |

---

## 3. الواجهات

| النمط | الاستخدام |
|-------|-----------|
| `financial-hub-page` | صفحات المالية والمستودع والتقارير |
| `operational-page` + `_OperationalSysHero` | لوحة التحكم، المشتركين، القطاعات، المستقبلات، الطلبات |
| `network-page-header` | إدارة الشبكات (مدير نظام) — نمط مخصص مقبول |

**صفحات ما زالت `page-header` كلاسيكي (أولوية منخفضة):** Sector/Receiver CRUD فرعية، Clients فرعية، JoinRequests، CustomServices، Profile، Notifications.

---

## 4. المسارات (Named Routes)

| المسار | الغرض |
|--------|--------|
| `networkManager-wallet-client-topups` | موافقة مدير الشركة |
| `collectionPoint-wallet-client-topups` | موافقة نقطة التحصيل |
| `collectionPoint-wallet-topup` | طلب تغذية محفظة CP |

---

## 5. خطة الوصول إلى ≥95%

1. تطبيق `_OperationalSysHero` على صفحات Sector/Receiver CRUD المتبقية (~12 ملف).
2. توثيق/تلميع `SystemAdmin` funding dashboard (بدون محفظة جديدة إن لم تُطلب).
3. (اختياري) رصيد هيدر لـ CollectionPoint و ClientPortal.
4. سجل معاملات موحّد — مشروع منفصل.

---

*مرجع الفجوات المالية: [WALLET-REQUIREMENTS-GAP.md](./WALLET-REQUIREMENTS-GAP.md)*
