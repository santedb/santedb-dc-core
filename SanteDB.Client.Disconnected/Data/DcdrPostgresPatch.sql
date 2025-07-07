/** 
 * <feature scope="SanteDB.Persistence.Data" id="DcdrPostgres-01" name="Update:DcdrPostgres-01"   invariantName="npgsql" environment="Gateway" >
 *	<summary>Update: Removes several problem issues when using PostgreSQL on the server.</summary>
 *	<isInstalled>select ck_patch('DcdrPostgres-01')</isInstalled>
 * </feature>
 */

-- OPTIONAL
ALTER TABLE mat_tbl DROP CONSTRAINT ck_mat_frm_cd;
--#!
-- OPTIONAL

ALTER TABLE mat_tbl DROP CONSTRAINT ck_mat_qty_cd;
--#!
-- OPTIONAL
ALTER TABLE sub_adm_tbl DROP CONSTRAINT ck_sub_adm_dos_unt_cd;
--#!
-- OPTIONAL
ALTER TABLE sub_adm_tbl DROP CONSTRAINT ck_sub_adm_rte_cd;
--#!
-- OPTIONAL
ALTER TABLE qty_obs_tbl DROP CONSTRAINT ck_qty_obs_uom_cd;
--#!
-- OPTIONAL

CREATE OR REPLACE FUNCTION ck_is_cd_set_mem(cd_id_in uuid, set_mnemonic_in character varying, allow_null_in boolean)
 RETURNS boolean
 LANGUAGE plpgsql
AS $function$
 BEGIN 
	RETURN TRUE;
 END;
$function$
;
--#!
-- OPTIONAL


SELECT REG_PATCH('DcdrPostgres-01');