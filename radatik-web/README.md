# RadaTik Web (`/app`)

واجهة React/TypeScript الحديثة ضمن مشروع RadaTik، تُبنى وتُنشر داخل:

- `RadaTik/wwwroot/app`

وتُخدَّم من الـ backend عبر المسار:

- `/app`

## تشغيل محلي

من مجلد `radatik-web`:

```bash
npm install
npm run dev
```

## تشغيل مع Mock API

```bash
npm run dev:mock
```

هذا يشغّل:

- `json-server` على المنفذ `3001`
- Vite dev server

## البناء للإنتاج

```bash
npm run build
```

الناتج يُكتب تلقائيًا إلى:

- `../RadaTik/wwwroot/app`

## الجودة

```bash
npm run lint
```

## المصادقة والتكامل مع MVC

- SPA تستخدم API: `/api/spa-auth/login` و`/api/spa-auth/logout`
- الاعتماد على Cookie auth من ASP.NET Core Identity
- تحديد الـ base URL يتم عبر `src/lib/mvcBaseUrl.ts`

## ملاحظات مهمة

- صلاحيات العرض داخل SPA تعتمد على الدور (`system_admin`, `company_manager`, `employee`, `client`, `collection_point`)
- أي تغييرات على الأدوار في backend يجب أن تُزامَن مع:
  - `src/types/index.ts`
  - `src/lib/roleI18n.ts`
  - `src/routes/RoleGate.tsx`
